namespace MonsterWebHost.Models;

public sealed class HostingMetrics
{
    public long RequestCount { get; init; }
    public long DownloadCount { get; init; }
    public long BytesServed { get; init; }
    public double RequestsPerSecond { get; init; }
    public int ActiveUsers { get; init; }
    public TimeSpan Uptime { get; init; }
    public DateTimeOffset? LastRequestAtUtc { get; init; }
}
