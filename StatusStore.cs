using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Thread-safe in-memory store for monitoring status of hosts.
/// Keeps the latest HostStatus for each target address and exposes
/// helper methods for updating and removing entries.
/// </summary>
public class StatusStore
{
    // Keyed by address
    private readonly ConcurrentDictionary<string, HostStatus> _statuses = new();
    private readonly int _warningThreshold;
    private readonly int _criticalThreshold;

    /// <summary>
    /// Reads thresholds from configuration and initializes the store.
    /// </summary>
    public StatusStore(IConfiguration configuration)
    {
        _warningThreshold = configuration.GetValue<int>("Monitoring:WarningThreshold", 1);
        _criticalThreshold = configuration.GetValue<int>("Monitoring:CriticalThreshold", 5);
        if (_warningThreshold < 1) _warningThreshold = 1;
        if (_criticalThreshold < _warningThreshold) _criticalThreshold = _warningThreshold;
    }

    /// <summary>
    /// Update or create the status entry for <paramref name="address"/>.
    /// This method increments the failure counter when the host is down,
    /// and resets it to zero when the host responds. It also computes the
    /// resulting severity (OK/Warning/Critical) based on configured thresholds.
    /// Returns the updated HostStatus and the prior severity so callers
    /// can detect transitions.
    /// </summary>
    public (HostStatus status, string previousSeverity) Set(string address, string? name, bool isUp, long latency)
    {
        var now = DateTime.UtcNow;
        _statuses.TryGetValue(address, out var existing);
        string previousSeverity = existing?.Severity ?? "OK";

        var result = _statuses.AddOrUpdate(address,
            addValueFactory: _ =>
            {
                var failures = isUp ? 0 : 1;
                var sev = ComputeSeverity(failures);
                return new HostStatus
                {
                    Address = address,
                    Name = name ?? string.Empty,
                    IsUp = isUp,
                    Latency = latency,
                    LastChecked = now,
                    FailureCount = failures,
                    Severity = sev
                };
            },
            updateValueFactory: (_, old) =>
            {
                var failures = old.FailureCount;
                if (isUp)
                {
                    failures = 0;
                }
                else
                {
                    failures = old.FailureCount + 1;
                }
                var sev = ComputeSeverity(failures);
                old.Address = address;
                old.Name = name ?? string.Empty;
                old.IsUp = isUp;
                old.Latency = latency;
                old.LastChecked = now;
                old.FailureCount = failures;
                old.Severity = sev;
                return old;
            });

        return (result, previousSeverity);
    }

    /// <summary>
    /// Compute severity label from a failure count using thresholds.
    /// </summary>
    private string ComputeSeverity(int failures)
    {
        if (failures <= 0) return "OK";
        if (failures >= _criticalThreshold) return "Critical";
        if (failures >= _warningThreshold) return "Warning";
        return "OK";
    }

    /// <summary>
    /// Returns a snapshot enumeration of current HostStatus values.
    /// </summary>
    public IEnumerable<HostStatus> GetAll() => _statuses.Values;

    /// <summary>
    /// Remove the status entry for the given address.
    /// Returns true if an entry was removed.
    /// </summary>
    public bool Remove(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        return _statuses.TryRemove(address, out _);
    }
}

/// <summary>
/// Represents the most recent observed status for a single host.
/// </summary>
public class HostStatus
{
    /// <summary>IP address or hostname being monitored (key).</summary>
    public string Address { get; set; } = "";
    /// <summary>Optional human-friendly name for the host.</summary>
    public string Name { get; set; } = "";
    /// <summary>Whether the last check reported the host as reachable.</summary>
    public bool IsUp { get; set; }
    /// <summary>Number of consecutive failed checks (resets on success).</summary>
    public int FailureCount { get; set; }
    /// <summary>Severity derived from failure count: OK/Warning/Critical.</summary>
    public string Severity { get; set; } = "OK";
    /// <summary>Last observed round-trip latency in milliseconds (-1 when unavailable).</summary>
    public long Latency { get; set; }
    /// <summary>UTC timestamp of the last check.</summary>
    public DateTime LastChecked { get; set; }
}
