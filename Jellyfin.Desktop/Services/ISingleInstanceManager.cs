namespace Jellyfin.Desktop.Services;

public interface ISingleInstanceManager
{
    bool IsFirstInstance { get; }
    Task SignalFirstInstanceAsync(string[] args);
}