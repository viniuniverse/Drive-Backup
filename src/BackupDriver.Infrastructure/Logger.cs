using System.Text.Json;

namespace BackupDriver.Infrastructure;

public enum LogLevel
{
    INFO,
    OK,
    WARN,
    ERROR
}

public class LogEntry
{
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string Level { get; set; } = LogLevel.INFO.ToString();
    public string Message { get; set; } = string.Empty;
}

public interface ILogger
{
    void Info(string message);
    void Ok(string message);
    void Warn(string message);
    void Error(string message);
    void Log(LogLevel level, string message);
    List<LogEntry> GetRecentLogs(int maxEntries = 1000);
}

public class Logger : ILogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly object _lock = new();
    private readonly List<LogEntry> _recentLogs = new();
    private readonly StreamWriter? _writer;

    public Logger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, $"backup_{DateTime.Now:yyyyMMdd}.jsonl");
        _writer = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
    }

    public void Info(string message) => Log(LogLevel.INFO, message);
    public void Ok(string message) => Log(LogLevel.OK, message);
    public void Warn(string message) => Log(LogLevel.WARN, message);
    public void Error(string message) => Log(LogLevel.ERROR, message);

    public void Log(LogLevel level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Level = level.ToString(),
            Message = message
        };

        lock (_lock)
        {
            _recentLogs.Add(entry);
            _writer?.WriteLine(JsonSerializer.Serialize(entry));
        }
    }

    public List<LogEntry> GetRecentLogs(int maxEntries = 1000)
    {
        lock (_lock)
        {
            return _recentLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(maxEntries)
                .ToList();
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
