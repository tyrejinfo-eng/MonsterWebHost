using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MonsterWebHost.Models;
using System.Threading.RateLimiting;

namespace MonsterWebHost.Services;

public sealed class WebsiteHostService : IAsyncDisposable
{
    private readonly LogStore _logStore;
    private readonly AnalyticsStore _analyticsStore;
    private readonly GeoLocationService _geoLocationService;
    private readonly HttpClient _probeClient = new();
    private WebApplication? _app;
    private string _folder = string.Empty;
    private int _port = 8080;
    private DateTimeOffset _startedAtUtc;
    private readonly ConcurrentQueue<DateTimeOffset> _requestTimestamps = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeenByIp = new();

    private long _requestCount;
    private long _downloadCount;
    private long _bytesServed;
    private DateTimeOffset? _lastRequestAt;

    private static readonly HashSet<string> DownloadExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".exe", ".msi", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".rar", ".7z", ".tar", ".gz", ".iso", ".apk", ".mp3", ".mp4", ".mov"
    };

    public WebsiteHostService(LogStore logStore, AnalyticsStore analyticsStore, GeoLocationService geoLocationService)
    {
        _logStore = logStore;
        _analyticsStore = analyticsStore;
        _geoLocationService = geoLocationService;
        _probeClient.Timeout = TimeSpan.FromSeconds(2);
    }

    public bool IsRunning => _app is not null;
    public string LocalUrl => IsRunning ? $"http://127.0.0.1:{_port}" : string.Empty;
    public string FolderPath => _folder;

    public long RequestCount => Interlocked.Read(ref _requestCount);
    public long DownloadCount => Interlocked.Read(ref _downloadCount);
    public long BytesServed => Interlocked.Read(ref _bytesServed);
    public double RequestsPerSecond => CalculateRequestsPerSecond();
    public int ActiveUsers => CalculateActiveUsers();
    public TimeSpan Uptime => IsRunning ? DateTimeOffset.UtcNow - _startedAtUtc : TimeSpan.Zero;
    public DateTimeOffset? LastRequestAt => _lastRequestAt;

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
        _startedAtUtc = DateTimeOffset.UtcNow;
        _requestTimestamps.Clear();
        _lastSeenByIp.Clear();
        Interlocked.Exchange(ref _requestCount, 0);
        Interlocked.Exchange(ref _downloadCount, 0);
        Interlocked.Exchange(ref _bytesServed, 0);
        _lastRequestAt = null;

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(WebsiteHostService).Assembly.FullName
        });

        var rateLimitPerMinute = builder.Configuration.GetValue("WebsiteHost:RateLimitPerMinute", 120);
        var enableHttpsRedirection = builder.Configuration.GetValue("WebsiteHost:EnableHttpsRedirection", false);
        var allowedIps = builder.Configuration.GetSection("WebsiteHost:AllowedIps").Get<string[]>() ?? [];
        var blockedIps = builder.Configuration.GetSection("WebsiteHost:BlockedIps").Get<string[]>() ?? [];

        builder.WebHost.UseKestrel();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = GetClientIp(context);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 10,
                        AutoReplenishment = true
                    });
            });
        });

        var app = builder.Build();
        app.UseRateLimiter();

        if (enableHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        var provider = new PhysicalFileProvider(folderPath);

        app.Use(async (context, next) =>
        {
            var clientIp = GetClientIp(context);

            if (blockedIps.Contains(clientIp, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden", cancellationToken);
                return;
            }

            if (allowedIps.Length > 0 && !allowedIps.Contains(clientIp, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden", cancellationToken);
                return;
            }

            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
            await next();
        });

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
            _lastSeenByIp[ip] = DateTimeOffset.UtcNow;
            _requestTimestamps.Enqueue(DateTimeOffset.UtcNow);
            TrimOldRequests();

            var geo = await _geoLocationService.LookupAsync(ip, cancellationToken);
            var responseBytes = context.Response.ContentLength ?? 0;
            var requestPath = context.Request.Path.HasValue ? context.Request.Path.Value ?? "/" : "/";
            var extension = Path.GetExtension(requestPath);
            var isDownload = DownloadExtensions.Contains(extension) || IsDownloadHeader(context.Response.Headers.ContentDisposition.ToString());
            var downloadFileName = isDownload ? Path.GetFileName(requestPath) : string.Empty;

            var trafficEvent = new TrafficEvent
            {
                SiteName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
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
                DownloadFileName = downloadFileName,
                GeoCountry = geo?.Country ?? string.Empty,
                GeoRegion = geo?.Region ?? string.Empty,
                GeoCity = geo?.City ?? string.Empty,
                GeoIsp = geo?.Isp ?? string.Empty,
                GeoOrg = geo?.Org ?? string.Empty,
                Latitude = geo?.Latitude,
                Longitude = geo?.Longitude
            };

            Interlocked.Increment(ref _requestCount);
            Interlocked.Add(ref _bytesServed, trafficEvent.ResponseBytes);
            if (trafficEvent.IsDownload)
            {
                Interlocked.Increment(ref _downloadCount);
            }

            _lastRequestAt = trafficEvent.TimestampUtc;

            TrafficLogged?.Invoke(this, trafficEvent);

            _ = _logStore.AppendAsync(trafficEvent, cancellationToken);
            _ = _analyticsStore.RecordTrafficAsync(trafficEvent, cancellationToken);
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

    public HostingMetrics GetMetrics()
    {
        return new HostingMetrics
        {
            RequestCount = RequestCount,
            DownloadCount = DownloadCount,
            BytesServed = BytesServed,
            RequestsPerSecond = RequestsPerSecond,
            ActiveUsers = ActiveUsers,
            Uptime = Uptime,
            LastRequestAtUtc = LastRequestAt
        };
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

    private double CalculateRequestsPerSecond()
    {
        TrimOldRequests();
        return _requestTimestamps.Count / 60.0;
    }

    private int CalculateActiveUsers()
    {
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-5);
        return _lastSeenByIp.Values.Count(v => v >= threshold);
    }

    private void TrimOldRequests()
    {
        var threshold = DateTimeOffset.UtcNow.AddMinutes(-1);
        while (_requestTimestamps.TryPeek(out var head) && head < threshold)
        {
            _requestTimestamps.TryDequeue(out _);
        }
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
