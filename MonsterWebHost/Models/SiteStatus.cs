namespace MonsterWebHost.Models;

public sealed class SiteStatus
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public bool IsRunning { get; init; }
    public string State { get; init; } = "Stopped";
}
