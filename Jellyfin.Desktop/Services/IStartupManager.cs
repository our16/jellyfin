namespace Jellyfin.Desktop.Services;

public interface IStartupManager
{
    bool IsEnabled { get; }
    void Enable();
    void Disable();
    void Toggle();
}