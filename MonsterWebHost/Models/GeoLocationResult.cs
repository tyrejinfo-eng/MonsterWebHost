namespace MonsterWebHost.Models;

public sealed class GeoLocationResult
{
    public string Ip { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Isp { get; init; } = string.Empty;
    public string Org { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}
