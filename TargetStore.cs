using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

public class TargetStore
{
    private readonly ConcurrentDictionary<string, MonitoringTarget> _targets = new();

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

    public IEnumerable<MonitoringTarget> GetAll() => _targets.Values;

    public bool TryAdd(MonitoringTarget t)
    {
        if (t == null || string.IsNullOrWhiteSpace(t.Address)) return false;
        return _targets.TryAdd(t.Address, t);
    }

    public bool TryRemove(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return false;
        return _targets.TryRemove(address, out _);
    }
}

public class MonitoringTarget
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
