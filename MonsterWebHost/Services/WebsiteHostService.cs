using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using MonsterWebHost.Models;

namespace MonsterWebHost.Services;

public sealed class WebsiteHostService : IAsyncDisposable
{
    private readonly LogStore _logStore;
    private readonly GeoLocationService _geoLocationService;
    private readonly HttpClient _probeClient = new();
    private WebApplication? _app;
    private string _folder = string.Empty;
    private int _port = 8080;

    private static readonly HashSet<string> DownloadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".exe", ".msi", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".rar", ".7z", ".tar", ".gz", ".iso", ".apk", ".mp3", ".mp4", ".mov"
    };

    public WebsiteHostService(LogStore logStore, GeoLocationService geoLocationService)
    {
        _logStore = logStore;
        _geoLocationService = geoLocationService;
        _probeClient.Timeout = TimeSpan.FromSeconds(2);
    }

    public bool IsRunning => _app is not null;
    public string LocalUrl => IsRunning ? $"http://127.0.0.1:{_port}" : string.Empty;
    public string FolderPath => _folder;

    public event EventHandler<TrafficEvent>? TrafficLogged;

    public async Task StartAsync(string folderPath, int port, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Folder path is required.", nameof(folderPath));
        }

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException(folderPath);
        }

        await StopAsync();
        _folder = folderPath;
        _port = port;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(WebsiteHostService).Assembly.FullName
        });

        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        var app = builder.Build();
        var provider = new PhysicalFileProvider(folderPath);

        app.Use(async (context, next) =>
        {
            var sw = Stopwatch.StartNew();
            await next();
            sw.Stop();

            if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var ip = GetClientIp(context);
            var geo = await _geoLocationService.LookupAsync(ip, cancellationToken);

            var responseBytes = context.Response.ContentLength ?? 0;
            var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value ?? "/" : "/";
            var extension = Path.GetExtension(requestPath);
            var isDownload = DownloadExtensions.Contains(extension) || IsDownloadHeader(context.Response.Headers.ContentDisposition.ToString());

            var trafficEvent = new TrafficEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                RequestMethod = context.Request.Method,
                RequestPath = requestPath,
                QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value ?? string.Empty : string.Empty,
                StatusCode = context.Response.StatusCode,
                ResponseBytes = responseBytes,
                DurationMs = sw.ElapsedMilliseconds,
                ClientIp = ip,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                Referer = context.Request.Headers.Referer.ToString(),
                IsDownload = isDownload,
                DownloadFileName = Path.GetFileName(requestPath),
                GeoCountry = geo?.Country ?? string.Empty,
                GeoRegion = geo?.Region ?? string.Empty,
                GeoCity = geo?.City ?? string.Empty,
                GeoIsp = geo?.Isp ?? string.Empty,
                GeoOrg = geo?.Org ?? string.Empty,
                Latitude = geo?.Latitude,
                Longitude = geo?.Longitude
            };

            TrafficLogged?.Invoke(this, trafficEvent);
            await _logStore.AppendAsync(trafficEvent, cancellationToken);
        });

        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = provider
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = provider,
            ServeUnknownFileTypes = true,
            DefaultContentType = "application/octet-stream"
        });

        app.MapGet("/health", () => Results.Ok(new
        {
            ok = true,
            folder = folderPath,
            port
        }));

        await app.StartAsync(cancellationToken);
        _app = app;

        await _logStore.AppendMessageAsync($"Website host started for {folderPath} on port {port}", cancellationToken);
    }

    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
        catch
        {
            // Swallow shutdown issues in the starter scaffold.
        }
        finally
        {
            _app = null;
            await _logStore.AppendMessageAsync("Website host stopped");
        }
    }

    public async Task<bool> PingHealthAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
        {
            return false;
        }

        try
        {
            using var response = await _probeClient.GetAsync($"{LocalUrl}/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _probeClient.Dispose();
    }

    private static string GetClientIp(HttpContext context)
    {
        var headers = context.Request.Headers;

        var candidates = new[]
        {
            headers["CF-Connecting-IP"].ToString(),
            headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim(),
            headers["True-Client-IP"].ToString(),
            context.Connection.RemoteIpAddress?.ToString()
        };

        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "unknown";
    }

    private static bool IsDownloadHeader(string contentDisposition)
    {
        return !string.IsNullOrWhiteSpace(contentDisposition) &&
               contentDisposition.Contains("attachment", StringComparison.OrdinalIgnoreCase);
    }
}
