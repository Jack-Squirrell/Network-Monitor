using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Holds the set of monitoring targets for the running application.
/// The store is populated from configuration at startup and can be
/// modified at runtime (Add/Remove) by the API endpoints.
/// </summary>
public class TargetStore
{
    private readonly ConcurrentDictionary<string, MonitoringTarget> _targets = new();

    /// <summary>
    /// Loads initial targets from configuration section "Monitoring:Targets".
    /// </summary>
    public TargetStore(IConfiguration configuration)
    {
        var targets = configuration.GetSection("Monitoring:Targets").Get<MonitoringTarget[]>();
        if (targets != null)
        {
            foreach (var t in targets)
            {
                if (t != null && !string.IsNullOrWhiteSpace(t.Address))
                {
                    _targets.TryAdd(t.Address, t);
                }
            }
        }
    }

    /// <summary>Return current targets as a snapshot enumeration.</summary>
    public IEnumerable<MonitoringTarget> GetAll() => _targets.Values;

    /// <summary>Attempt to add a target at runtime. Returns false if invalid or exists.</summary>
    public bool TryAdd(MonitoringTarget t)
    {
        if (t == null || string.IsNullOrWhiteSpace(t.Address)) return false;
        return _targets.TryAdd(t.Address, t);
    }

    /// <summary>Attempt to remove a target by address. Returns true if removed.</summary>
    public bool TryRemove(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        return _targets.TryRemove(address, out _);
    }
}

/// <summary>
/// Represents a target to monitor: a user-friendly name and an address.
/// </summary>
public class MonitoringTarget
{
    /// <summary>Optional display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>IP address or hostname to ping.</summary>
    public string Address { get; set; } = string.Empty;
}
