using System.Diagnostics;

namespace MonsterWebHost.Services;

public sealed class CloudflaredService : IDisposable
{
    private readonly LogStore _logStore;
    private Process? _process;

    public CloudflaredService(LogStore logStore)
    {
        _logStore = logStore;
    }

    public bool IsRunning => _process is not null && !_process.HasExited;
    public string LastOutput { get; private set; } = string.Empty;

    public event Action<string>? OutputReceived;

    public async Task StartAsync(string executablePath, string arguments, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Cloudflared executable path is required.", nameof(executablePath));
        }

        var psi = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                LastOutput = e.Data;
                OutputReceived?.Invoke(e.Data);
                _ = _logStore.AppendMessageAsync($"[cloudflared] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                LastOutput = e.Data;
                OutputReceived?.Invoke(e.Data);
                _ = _logStore.AppendMessageAsync($"[cloudflared:err] {e.Data}");
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Cloudflared could not be started.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _process = process;

        _ = _logStore.AppendMessageAsync($"Cloudflared started: {executablePath} {arguments}");
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch
        {
        }
        finally
        {
            _process.Dispose();
            _process = null;
            _ = _logStore.AppendMessageAsync("Cloudflared stopped");
        }
    }

    public void Dispose()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            _process.Dispose();
            _process = null;
        }
    }
}
