using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.NetworkInformation;
using System.Collections.Concurrent;
using System.Linq;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly StatusStore _statusStore;
    private readonly LogStore _logStore;

    private readonly int _intervalSeconds;
    private readonly TargetStore _targetStore;

    public Worker(ILogger<Worker> logger, StatusStore statusStore, LogStore logStore, TargetStore targetStore, IConfiguration configuration)
    {
        _logger = logger;
        _statusStore = statusStore;
        _logStore = logStore;
        _targetStore = targetStore;
        _intervalSeconds = configuration.GetValue<int>("Monitoring:IntervalSeconds", 60);
    }

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
