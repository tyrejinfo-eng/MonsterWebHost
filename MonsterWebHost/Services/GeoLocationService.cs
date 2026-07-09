using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MonsterWebHost.Models;

namespace MonsterWebHost.Services;

public sealed class GeoLocationService
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, GeoLocationResult?> _cache = new();

    public GeoLocationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(4);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MonsterWebHost/1.0");
    }

    public async Task<GeoLocationResult?> LookupAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return null;
        }

        if (_cache.TryGetValue(ipAddress, out var cached))
        {
            return cached;
        }

        try
        {
            var url = $"https://ip-api.com/json/{Uri.EscapeDataString(ipAddress)}?fields=status,message,country,regionName,city,lat,lon,query,isp,org,timezone";
            var response = await _httpClient.GetFromJsonAsync<IpApiResponse>(url, cancellationToken);
            if (response is null || !string.Equals(response.Status, "success", StringComparison.OrdinalIgnoreCase))
            {
                _cache[ipAddress] = null;
                return null;
            }

            var result = new GeoLocationResult
            {
                Ip = response.Query ?? ipAddress,
                Country = response.Country ?? string.Empty,
                Region = response.RegionName ?? string.Empty,
                City = response.City ?? string.Empty,
                Isp = response.Isp ?? string.Empty,
                Org = response.Org ?? string.Empty,
                Timezone = response.Timezone ?? string.Empty,
                Latitude = response.Lat,
                Longitude = response.Lon
            };

            _cache[ipAddress] = result;
            return result;
        }
        catch
        {
            _cache[ipAddress] = null;
            return null;
        }
    }

    private sealed class IpApiResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("regionName")]
        public string? RegionName { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("lat")]
        public double? Lat { get; set; }

        [JsonPropertyName("lon")]
        public double? Lon { get; set; }

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("isp")]
        public string? Isp { get; set; }

        [JsonPropertyName("org")]
        public string? Org { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }
    }
}
