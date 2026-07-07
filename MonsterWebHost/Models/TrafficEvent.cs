namespace MonsterWebHost.Models;

public sealed class TrafficEvent
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public string RequestMethod { get; init; } = "GET";
    public string RequestPath { get; init; } = "/";
    public string QueryString { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long ResponseBytes { get; init; }
    public long DurationMs { get; init; }
    public string ClientIp { get; init; } = "unknown";
    public string UserAgent { get; init; } = string.Empty;
    public string Referer { get; init; } = string.Empty;
    public bool IsDownload { get; init; }
    public string DownloadFileName { get; init; } = string.Empty;
    public string GeoCountry { get; init; } = string.Empty;
    public string GeoRegion { get; init; } = string.Empty;
    public string GeoCity { get; init; } = string.Empty;
    public string GeoIsp { get; init; } = string.Empty;
    public string GeoOrg { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public string TimestampLocalText => TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
    public string ResponseBytesHuman => FormatBytes(ResponseBytes);
    public string DurationText => $"{DurationMs} ms";
    public string GeoSummary
    {
        get
        {
            var pieces = new[] { GeoCity, GeoRegion, GeoCountry }.Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(", ", pieces);
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var len = bytes;
        int order = 0;
        while (len >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:n0} {suffixes[order]}";
    }
}
