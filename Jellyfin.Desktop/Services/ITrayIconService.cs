using System.Drawing;

namespace Jellyfin.Desktop.Services;

public interface ITrayIconService : IDisposable
{
    void Initialize();
    void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000);
    void UpdateStatus(string status, Icon? icon = null);
    event Action? OnOpenWebInterface;
    event Action? OnRestartServer;
    event Action? OnStopServer;
    event Action? OnStartServer;
    event Action? OnOpenSettings;
    event Action? OnExit;
    bool IsServerRunning { get; set; }
    string ServerUrl { get; set; }
}