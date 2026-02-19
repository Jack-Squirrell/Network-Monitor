using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using System.Linq;

/// <summary>
/// Background service that periodically pings the configured targets.
/// It queries the runtime `TargetStore` for targets, performs concurrent
/// ping checks, and writes results into the `StatusStore` and `LogStore`.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly StatusStore _statusStore;
    private readonly LogStore _logStore;

    private readonly int _intervalSeconds;
    private readonly TargetStore _targetStore;

    /// <summary>
    /// Constructs the Worker.
    /// </summary>
    /// <param name="logger">Logger for informational/warning/critical messages.</param>
    /// <param name="statusStore">StatusStore to persist host statuses.</param>
    /// <param name="logStore">LogStore to persist recent log messages.</param>
    /// <param name="targetStore">TargetStore providing the list of targets to monitor.</param>
    /// <param name="configuration">Configuration (used to read interval).</param>
    public Worker(ILogger<Worker> logger, StatusStore statusStore, LogStore logStore, TargetStore targetStore, IConfiguration configuration)
    {
        _logger = logger;
        _statusStore = statusStore;
        _logStore = logStore;
        _targetStore = targetStore;
        _intervalSeconds = configuration.GetValue<int>("Monitoring:IntervalSeconds", 60);
    }

    /// <summary>
    /// Main background loop. Kicks off checks for all targets in parallel
    /// then waits for the configured interval before repeating.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Network Monitor Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var tasks = _targetStore.GetAll().Select(t => CheckHostAsync(t, stoppingToken));
            await Task.WhenAll(tasks);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }

    /// <summary>
    /// Checks a single host by performing a ping and updating the
    /// <see cref="StatusStore"/> with the results. Emits logs on severity
    /// transitions (Warning/Critical) and on recovery.
    /// </summary>
    /// <param name="target">The target to ping.</param>
    /// <param name="token">Cancellation token for cooperative shutdown.</param>
    private async Task CheckHostAsync(MonitoringTarget target, CancellationToken token)
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(target.Address, 2000);

            bool isUp = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            long latency = isUp ? reply.RoundtripTime : -1;

            var (status, previousSeverity) = _statusStore.Set(target.Address, target.Name, isUp, latency);

            string display = string.IsNullOrEmpty(target.Name) ? target.Address : $"{target.Name} ({target.Address})";

            // Log transitions: to Warning, to Critical, and recovery back to OK
            if (!isUp)
            {
                if (status.Severity != previousSeverity)
                {
                    if (status.Severity == "Warning")
                    {
                        string msg = $"{DateTime.Now:T} - {display} is WARNING (failures={status.FailureCount})";
                        _logger.LogWarning(msg);
                        _logStore.Add(msg);
                    }
                    else if (status.Severity == "Critical")
                    {
                        string msg = $"{DateTime.Now:T} - {display} is CRITICAL (failures={status.FailureCount})";
                        _logger.LogCritical(msg);
                        _logStore.Add(msg);
                    }
                }
            }
            else
            {
                if (previousSeverity != "OK")
                {
                    string msg = $"{DateTime.Now:T} - {display} recovered";
                    _logger.LogInformation(msg);
                    _logStore.Add(msg);
                }
            }
        }
        catch (Exception ex)
        {
            string display = string.IsNullOrEmpty(target.Name) ? target.Address : $"{target.Name} ({target.Address})";
            string errMsg = $"{DateTime.Now:T} - Error pinging {display}: {ex.Message}";
            _logger.LogError(ex, errMsg);
            _logStore.Add(errMsg);

            var (status, previousSeverity) = _statusStore.Set(target.Address, target.Name, false, -1);
            if (status.Severity != previousSeverity)
            {
                if (status.Severity == "Warning")
                {
                    string msg = $"{DateTime.Now:T} - {display} is WARNING (failures={status.FailureCount})";
                    _logger.LogWarning(msg);
                    _logStore.Add(msg);
                }
                else if (status.Severity == "Critical")
                {
                    string msg = $"{DateTime.Now:T} - {display} is CRITICAL (failures={status.FailureCount})";
                    _logger.LogCritical(msg);
                    _logStore.Add(msg);
                }
            }
        }
    }

    // MonitoringTarget is defined in TargetStore
}
