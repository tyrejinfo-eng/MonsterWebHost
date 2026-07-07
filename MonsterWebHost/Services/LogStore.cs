using System.Text.Json;
using MonsterWebHost.Models;

namespace MonsterWebHost.Services;

public sealed class LogStore
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _logDirectory;
    private readonly string _requestLogPath;

    public LogStore()
    {
        _logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonsterWebHost", "Logs");
        Directory.CreateDirectory(_logDirectory);
        _requestLogPath = Path.Combine(_logDirectory, $"requests-{DateTime.UtcNow:yyyyMMdd}.jsonl");
    }

    public string LogDirectory => _logDirectory;

    public async Task AppendAsync(TrafficEvent trafficEvent, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(trafficEvent, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_requestLogPath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task AppendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            message
        };

        var line = JsonSerializer.Serialize(payload);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var path = Path.Combine(_logDirectory, $"messages-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            await File.AppendAllTextAsync(path, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }
}
