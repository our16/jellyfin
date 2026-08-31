using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Jellyfin.Desktop.Services;

public sealed class StartupManager : IStartupManager
{
    private const string AppName = "JellyfinDesktop";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly ILogger<StartupManager> _logger;

    public StartupManager(ILogger<StartupManager> logger)
    {
        _logger = logger;
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                var value = key?.GetValue(AppName) as string;
                var exePath = GetExecutablePath();
                return !string.IsNullOrEmpty(value) && string.Equals(value, exePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check startup registration");
                return false;
            }
        }
    }

    public void Enable()
    {
        try
        {
            var exePath = GetExecutablePath();
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.SetValue(AppName, exePath, RegistryValueKind.String);
            _logger.LogInformation("Enabled auto-start at login: {Path}", exePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable auto-start");
            throw;
        }
    }

    public void Disable()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            key?.DeleteValue(AppName, false);
            _logger.LogInformation("Disabled auto-start at login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable auto-start");
            throw;
        }
    }

    public void Toggle()
    {
        if (IsEnabled)
            Disable();
        else
            Enable();
    }

    private static string GetExecutablePath()
    {
        var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory + "Jellyfin.Desktop.exe";
        if (string.IsNullOrEmpty(exePath))
        {
            exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "Jellyfin.Desktop.exe";
        }
        return $"\"{exePath}\" --minimized";
    }
}