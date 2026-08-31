namespace Jellyfin.Desktop.Configuration;

public sealed class DesktopOptions
{
    public string ServerExecutableName { get; set; } = "jellyfin.exe";
    public string ServerArguments { get; set; } = "--webdir ./jellyfin-web/dist";
    public bool AutoStartServer { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ShowStartupBalloon { get; set; } = true;
    public int HealthCheckIntervalSeconds { get; set; } = 30;
    public bool RestartOnCrash { get; set; } = true;
    public int MaxRestartAttempts { get; set; } = 3;
    public int RestartDelaySeconds { get; set; } = 5;
    public bool StartAtLogin { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool RunAsService { get; set; } = false;
    public string ServiceName { get; set; } = "Jellyfin";
    public string ServiceDisplayName { get; set; } = "Jellyfin Media Server";
    public string ServiceDescription { get; set; } = "The Free Software Media System";
}