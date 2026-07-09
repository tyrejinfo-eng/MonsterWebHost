namespace MonsterWebHost.Models;

public sealed class SiteConfiguration
{
    public string Name { get; set; } = "Default Site";
    public string FolderPath { get; set; } = string.Empty;
    public int Port { get; set; } = 8080;
    public string HostName { get; set; } = "127.0.0.1";
    public bool EnableHttpsRedirection { get; set; }
    public bool EnableCloudflared { get; set; }
}
