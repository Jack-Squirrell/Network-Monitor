using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting.WindowsServices;

// Create builder
var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows() && !Environment.UserInteractive)
{
    builder.Host.UseWindowsService();
}

// Register singletons
builder.Services.AddSingleton<StatusStore>();
builder.Services.AddSingleton<LogStore>();
// Register target store (loads initial targets from config)
builder.Services.AddSingleton<TargetStore>();

// Register Worker as hosted service (DI injects logger, StatusStore, LogStore, TargetStore)
builder.Services.AddHostedService<Worker>();

// Make LocalHost
builder.WebHost.UseUrls("http://127.0.0.1:8080");


var app = builder.Build();

// Status API
app.MapGet("/api/status", (StatusStore store) => store.GetAll());

// Targets API
app.MapGet("/api/targets", (TargetStore ts) => ts.GetAll());

app.MapPost("/api/targets", async (TargetStore ts, HttpRequest req) =>
{
    var t = await req.ReadFromJsonAsync<MonitoringTarget>();
    if (t == null || string.IsNullOrWhiteSpace(t.Address)) return Results.BadRequest();
    // Validate address (allow IPs and simple hostnames)
    bool IsValidHost(string h)
    {
        if (System.Net.IPAddress.TryParse(h, out _)) return true;
        var rx = new System.Text.RegularExpressions.Regex("^[A-Za-z0-9](?:[A-Za-z0-9\\-\\.]{0,253}[A-Za-z0-9])?$");
        return rx.IsMatch(h);
    }

    if (!IsValidHost(t.Address)) return Results.BadRequest();

    // Try add to in-memory store first
    var added = ts.TryAdd(t);
    if (!added) return Results.Conflict("Target already exists");

    // Persist to appsettings.json; rollback in-memory if persistence or replace fails
    try
    {
        var file = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        var text = File.Exists(file) ? await File.ReadAllTextAsync(file) : "{}";
        var root = System.Text.Json.Nodes.JsonNode.Parse(text) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();

        var monitoring = root["Monitoring"] as System.Text.Json.Nodes.JsonObject;
        if (monitoring == null)
        {
            monitoring = new System.Text.Json.Nodes.JsonObject();
            root["Monitoring"] = monitoring;
        }

        var targets = monitoring["Targets"] as System.Text.Json.Nodes.JsonArray;
        if (targets == null)
        {
            targets = new System.Text.Json.Nodes.JsonArray();
            monitoring["Targets"] = targets;
        }

        // Add new target object
        var obj = new System.Text.Json.Nodes.JsonObject
        {
            ["Name"] = t.Name,
            ["Address"] = t.Address
        };

        targets.Add(obj);

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var temp = file + ".tmp";
        await File.WriteAllTextAsync(temp, root.ToJsonString(options));

        // Replace atomically
        if (File.Exists(file))
        {
            var backup = file + ".bak";
            File.Replace(temp, file, backup);
            try { File.Delete(backup); } catch { }
        }
        else
        {
            File.Move(temp, file);
        }
    }
    catch (Exception)
    {
        // rollback in-memory addition
        ts.TryRemove(t.Address);
        return Results.StatusCode(500);
    }

    return Results.Created($"/api/targets/{t.Address}", t);
});

// Delete a target
app.MapDelete("/api/targets/{address}", async (TargetStore ts, StatusStore statusStore, string address) =>
{
    if (string.IsNullOrWhiteSpace(address)) return Results.BadRequest();

    // find existing target
    var existing = ts.GetAll().FirstOrDefault(x => string.Equals(x.Address, address, StringComparison.OrdinalIgnoreCase));
    if (existing == null) return Results.NotFound();

    // remove in-memory
    var removed = ts.TryRemove(address);
    if (!removed) return Results.NotFound();

    // Persist removal to appsettings.json; rollback if persistence fails
    try
    {
        var file = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        var text = File.Exists(file) ? await File.ReadAllTextAsync(file) : "{}";
        var root = System.Text.Json.Nodes.JsonNode.Parse(text) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();

        var monitoring = root["Monitoring"] as System.Text.Json.Nodes.JsonObject;
        if (monitoring != null)
        {
            var targets = monitoring["Targets"] as System.Text.Json.Nodes.JsonArray;
            if (targets != null)
            {
                var toRemove = targets.Where(n =>
                {
                    if (n is System.Text.Json.Nodes.JsonValue v) return v.ToString().Trim('"') == address;
                    if (n is System.Text.Json.Nodes.JsonObject o)
                    {
                        var a = o["Address"]?.ToString()?.Trim('"');
                        return a == address;
                    }
                    return false;
                }).ToList();

                foreach (var node in toRemove) targets.Remove(node);
            }
        }

        var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        var temp = file + ".tmp";
        await File.WriteAllTextAsync(temp, root.ToJsonString(options));

        if (File.Exists(file))
        {
            var backup = file + ".bak";
            File.Replace(temp, file, backup);
            try { File.Delete(backup); } catch { }
        }
        else
        {
            File.Move(temp, file);
        }
    }
    catch (Exception)
    {
        // rollback in-memory removal
        ts.TryAdd(existing);
        return Results.StatusCode(500);
    }

    // Remove stored status so UI reflects removal immediately
    statusStore.Remove(address);

    return Results.Ok();
});

// Logs API
app.MapGet("/api/logs", (LogStore logStore) => logStore.GetAll());

// Serve dashboard
app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
