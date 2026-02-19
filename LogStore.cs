using System.Collections.Concurrent;

/// <summary>
/// Simple thread-safe in-memory ring buffer for text log lines.
/// Keeps up to 100 most recent entries.
/// </summary>
public class LogStore
{
    private readonly ConcurrentQueue<string> _logs = new();

    /// <summary>
    /// Add a log message to the queue. If the queue exceeds the maximum
    /// size it will drop the oldest entries.
    /// </summary>
    public void Add(string log)
    {
        _logs.Enqueue(log);
        while (_logs.Count > 100 && _logs.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Returns the stored log messages in newest-to-oldest order.
    /// </summary>
    public IEnumerable<string> GetAll() => _logs.Reverse();
}
