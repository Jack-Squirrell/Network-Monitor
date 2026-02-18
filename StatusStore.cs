using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

public class StatusStore
{
    private readonly ConcurrentDictionary<string, HostStatus> _statuses = new();
    private readonly int _warningThreshold;
    private readonly int _criticalThreshold;

    public StatusStore(IConfiguration configuration)
    {
        _warningThreshold = configuration.GetValue<int>("Monitoring:WarningThreshold", 1);
        _criticalThreshold = configuration.GetValue<int>("Monitoring:CriticalThreshold", 5);
        if (_warningThreshold < 1) _warningThreshold = 1;
        if (_criticalThreshold < _warningThreshold) _criticalThreshold = _warningThreshold;
    }

    // Set updates the status and failure count. Returns the updated status and the previous severity.
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

    private string ComputeSeverity(int failures)
    {
        if (failures <= 0) return "OK";
        if (failures >= _criticalThreshold) return "Critical";
        if (failures >= _warningThreshold) return "Warning";
        return "OK";
    }

    public IEnumerable<HostStatus> GetAll() => _statuses.Values;

    public bool Remove(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        return _statuses.TryRemove(address, out _);
    }
}

public class HostStatus
{
    public string Address { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsUp { get; set; }
    public int FailureCount { get; set; }
    public string Severity { get; set; } = "OK";
    public long Latency { get; set; }
    public DateTime LastChecked { get; set; }
}
