namespace MonsterWebHost.Models;

public sealed class WebsiteState
{
    public bool IsHostRunning { get; init; }
    public bool IsOnline { get; init; }
    public string LocalUrl { get; init; } = string.Empty;
    public string CloudflaredStatus { get; init; } = "Cloudflared stopped";
}
