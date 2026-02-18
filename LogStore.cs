using System.Collections.Concurrent;

public class LogStore
{
    private readonly ConcurrentQueue<string> _logs = new();

    public void Add(string log)
    {
        _logs.Enqueue(log);
        while (_logs.Count > 100 && _logs.TryDequeue(out _)) { }
    }

    public IEnumerable<string> GetAll() => _logs.Reverse();
}
