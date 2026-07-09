using Microsoft.Data.Sqlite;
using MonsterWebHost.Models;

namespace MonsterWebHost.Services;

public sealed class AnalyticsStore : IDisposable
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _dbLock = new(1, 1);
    private bool _disposed;

    public AnalyticsStore()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonsterWebHost", "Data");
        Directory.CreateDirectory(root);
        _databasePath = Path.Combine(root, "analytics.db");
        Initialize();
    }

    public string DatabasePath => _databasePath;

    private void Initialize()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Requests (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SiteName TEXT NOT NULL,
                TimestampUtc TEXT NOT NULL,
                RequestMethod TEXT NOT NULL,
                RequestPath TEXT NOT NULL,
                QueryString TEXT NOT NULL,
                StatusCode INTEGER NOT NULL,
                ResponseBytes INTEGER NOT NULL,
                DurationMs INTEGER NOT NULL,
                ClientIp TEXT NOT NULL,
                UserAgent TEXT NOT NULL,
                Referer TEXT NOT NULL,
                IsDownload INTEGER NOT NULL,
                DownloadFileName TEXT NOT NULL,
                GeoCountry TEXT NOT NULL,
                GeoRegion TEXT NOT NULL,
                GeoCity TEXT NOT NULL,
                GeoIsp TEXT NOT NULL,
                GeoOrg TEXT NOT NULL,
                Latitude REAL NULL,
                Longitude REAL NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Requests_TimestampUtc ON Requests(TimestampUtc);
            CREATE INDEX IF NOT EXISTS IX_Requests_SiteName ON Requests(SiteName);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection CreateConnection() => new($"Data Source={_databasePath}");

    public async Task RecordTrafficAsync(TrafficEvent trafficEvent, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Requests
                (SiteName, TimestampUtc, RequestMethod, RequestPath, QueryString, StatusCode, ResponseBytes, DurationMs,
                 ClientIp, UserAgent, Referer, IsDownload, DownloadFileName, GeoCountry, GeoRegion, GeoCity, GeoIsp, GeoOrg,
                 Latitude, Longitude)
                VALUES
                ($SiteName, $TimestampUtc, $RequestMethod, $RequestPath, $QueryString, $StatusCode, $ResponseBytes, $DurationMs,
                 $ClientIp, $UserAgent, $Referer, $IsDownload, $DownloadFileName, $GeoCountry, $GeoRegion, $GeoCity, $GeoIsp, $GeoOrg,
                 $Latitude, $Longitude);
                """;

            command.Parameters.AddWithValue("$SiteName", trafficEvent.SiteName);
            command.Parameters.AddWithValue("$TimestampUtc", trafficEvent.TimestampUtc.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("$RequestMethod", trafficEvent.RequestMethod);
            command.Parameters.AddWithValue("$RequestPath", trafficEvent.RequestPath);
            command.Parameters.AddWithValue("$QueryString", trafficEvent.QueryString);
            command.Parameters.AddWithValue("$StatusCode", trafficEvent.StatusCode);
            command.Parameters.AddWithValue("$ResponseBytes", trafficEvent.ResponseBytes);
            command.Parameters.AddWithValue("$DurationMs", trafficEvent.DurationMs);
            command.Parameters.AddWithValue("$ClientIp", trafficEvent.ClientIp);
            command.Parameters.AddWithValue("$UserAgent", trafficEvent.UserAgent);
            command.Parameters.AddWithValue("$Referer", trafficEvent.Referer);
            command.Parameters.AddWithValue("$IsDownload", trafficEvent.IsDownload ? 1 : 0);
            command.Parameters.AddWithValue("$DownloadFileName", trafficEvent.DownloadFileName);
            command.Parameters.AddWithValue("$GeoCountry", trafficEvent.GeoCountry);
            command.Parameters.AddWithValue("$GeoRegion", trafficEvent.GeoRegion);
            command.Parameters.AddWithValue("$GeoCity", trafficEvent.GeoCity);
            command.Parameters.AddWithValue("$GeoIsp", trafficEvent.GeoIsp);
            command.Parameters.AddWithValue("$GeoOrg", trafficEvent.GeoOrg);
            command.Parameters.AddWithValue("$Latitude", (object?)trafficEvent.Latitude ?? DBNull.Value);
            command.Parameters.AddWithValue("$Longitude", (object?)trafficEvent.Longitude ?? DBNull.Value);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
        }
        finally
        {
            _dbLock.Release();
        }
    }

    public async Task<IReadOnlyList<TrafficEvent>> ReadRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var items = new List<TrafficEvent>();

        await _dbLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT SiteName, TimestampUtc, RequestMethod, RequestPath, QueryString, StatusCode, ResponseBytes, DurationMs,
                       ClientIp, UserAgent, Referer, IsDownload, DownloadFileName, GeoCountry, GeoRegion, GeoCity, GeoIsp, GeoOrg,
                       Latitude, Longitude
                FROM Requests
                ORDER BY TimestampUtc DESC
                LIMIT $Limit;
                """;
            command.Parameters.AddWithValue("$Limit", limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new TrafficEvent
                {
                    SiteName = reader.GetString(0),
                    TimestampUtc = DateTimeOffset.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    RequestMethod = reader.GetString(2),
                    RequestPath = reader.GetString(3),
                    QueryString = reader.GetString(4),
                    StatusCode = reader.GetInt32(5),
                    ResponseBytes = reader.GetInt64(6),
                    DurationMs = reader.GetInt64(7),
                    ClientIp = reader.GetString(8),
                    UserAgent = reader.GetString(9),
                    Referer = reader.GetString(10),
                    IsDownload = reader.GetInt64(11) == 1,
                    DownloadFileName = reader.GetString(12),
                    GeoCountry = reader.GetString(13),
                    GeoRegion = reader.GetString(14),
                    GeoCity = reader.GetString(15),
                    GeoIsp = reader.GetString(16),
                    GeoOrg = reader.GetString(17),
                    Latitude = reader.IsDBNull(18) ? null : reader.GetDouble(18),
                    Longitude = reader.IsDBNull(19) ? null : reader.GetDouble(19)
                });
            }
        }
        catch
        {
            return items;
        }
        finally
        {
            _dbLock.Release();
        }

        return items;
    }

    public async Task ExportCsvAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var items = await ReadRecentAsync(int.MaxValue, cancellationToken);

        static string Q(string value)
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        var lines = new List<string>
        {
            "SiteName,TimestampUtc,RequestMethod,RequestPath,QueryString,StatusCode,ResponseBytes,DurationMs,ClientIp,UserAgent,Referer,IsDownload,DownloadFileName,GeoCountry,GeoRegion,GeoCity,GeoIsp,GeoOrg,Latitude,Longitude"
        };

        foreach (var item in items)
        {
            lines.Add(string.Join(",",
                Q(item.SiteName),
                Q(item.TimestampUtc.ToString("O")),
                Q(item.RequestMethod),
                Q(item.RequestPath),
                Q(item.QueryString),
                item.StatusCode,
                item.ResponseBytes,
                item.DurationMs,
                Q(item.ClientIp),
                Q(item.UserAgent),
                Q(item.Referer),
                item.IsDownload ? 1 : 0,
                Q(item.DownloadFileName),
                Q(item.GeoCountry),
                Q(item.GeoRegion),
                Q(item.GeoCity),
                Q(item.GeoIsp),
                Q(item.GeoOrg),
                item.Latitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                item.Longitude?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
        }

        await File.WriteAllLinesAsync(outputPath, lines, cancellationToken);
    }

    public async Task ExportJsonAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var items = await ReadRecentAsync(int.MaxValue, cancellationToken);
        var json = System.Text.Json.JsonSerializer.Serialize(items, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, cancellationToken);
    }

    public void Dispose()
    {
        _disposed = true;
        _dbLock.Dispose();
    }
}
