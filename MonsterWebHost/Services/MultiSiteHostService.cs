using System.Collections.Concurrent;

namespace MonsterWebHost.Services;

public sealed class MultiSiteHostService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, WebsiteHostService> _sites = new();
    private readonly LogStore _logStore;
    private readonly AnalyticsStore _analyticsStore;
    private readonly GeoLocationService _geoLocationService;

    public MultiSiteHostService(LogStore logStore, AnalyticsStore analyticsStore, GeoLocationService geoLocationService)
    {
        _logStore = logStore;
        _analyticsStore = analyticsStore;
        _geoLocationService = geoLocationService;
    }

    public IReadOnlyCollection<string> SiteKeys => _sites.Keys.ToArray();

    public async Task StartSiteAsync(string key, string folderPath, int port, CancellationToken cancellationToken = default)
    {
        var host = new WebsiteHostService(_logStore, _analyticsStore, _geoLocationService);
        if (_sites.TryAdd(key, host))
        {
            await host.StartAsync(folderPath, port, cancellationToken);
            return;
        }

        await _sites[key].StartAsync(folderPath, port, cancellationToken);
    }

    public async Task StopSiteAsync(string key)
    {
        if (_sites.TryRemove(key, out var host))
        {
            await host.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var key in _sites.Keys.ToArray())
        {
            await StopSiteAsync(key);
        }
    }
}
