using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Jellyfin.Desktop.Configuration;
using Jellyfin.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Desktop;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ILogger<TrayApplicationContext> _logger;
    private readonly IServiceProvider _services;
    private readonly ITrayIconService _trayIcon;
    private readonly IServerProcessManager _serverManager;
    private readonly IStartupManager _startupManager;
    private readonly DesktopOptions _options;
    private SettingsForm? _settingsForm;
    private bool _isExiting;
    private Form? _uiForm;

    public TrayApplicationContext(
        ILogger<TrayApplicationContext> logger,
        IServiceProvider services,
        ITrayIconService trayIcon,
        IServerProcessManager serverManager,
        IStartupManager startupManager,
        IOptions<DesktopOptions> options)
    {
        _logger = logger;
        _services = services;
        _trayIcon = trayIcon;
        _serverManager = serverManager;
        _startupManager = startupManager;
        _options = options.Value;

        _trayIcon.OnOpenWebInterface += OpenWebInterface;
        _trayIcon.OnRestartServer += RestartServer;
        _trayIcon.OnStopServer += StopServer;
        _trayIcon.OnStartServer += StartServer;
        _trayIcon.OnOpenSettings += OpenSettings;
        _trayIcon.OnExit += ExitApplication;

        _serverManager.OnServerStatusChanged += OnServerStatusChanged;
        _serverManager.OnServerOutput += OnServerOutput;

        _trayIcon.Initialize();
        _trayIcon.ServerUrl = _serverManager.GetServerUrl();

        // Create a hidden form for UI thread marshaling
        _uiForm = new Form { Visible = false, ShowInTaskbar = false };
        _uiForm.CreateControl();

        if (_options.AutoStartServer)
        {
            _ = Task.Run(() => StartServer());
        }

        if (_options.StartAtLogin)
        {
            _startupManager.Enable();
        }

        _logger.LogInformation("Tray application context initialized");
    }

    private void OnServerStatusChanged(bool isRunning)
    {
        if (_uiForm?.InvokeRequired == true)
        {
            _uiForm.BeginInvoke(() => OnServerStatusChanged(isRunning));
            return;
        }

        _trayIcon.IsServerRunning = isRunning;
        _trayIcon.UpdateStatus(isRunning ? "运行中" : "已停止");
    }

    private void OnServerOutput(string output)
    {
        _logger.LogDebug("Server: {Output}", output);
    }

    private void OpenWebInterface()
    {
        try
        {
            var url = _serverManager.GetServerUrl();
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            _logger.LogInformation("Opened web interface: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open web interface");
            _trayIcon.ShowBalloonTip("打开失败", $"无法打开浏览器: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private async void StartServer()
    {
        _trayIcon.ShowBalloonTip("Jellyfin", "正在启动服务...", ToolTipIcon.Info, 2000);
        var success = await _serverManager.StartAsync();
        if (!success)
        {
            _trayIcon.ShowBalloonTip("启动失败", "服务启动失败，请查看日志", ToolTipIcon.Error);
        }
    }

    private async void StopServer()
    {
        _trayIcon.ShowBalloonTip("Jellyfin", "正在停止服务...", ToolTipIcon.Info, 2000);
        await _serverManager.StopAsync();
    }

    private async void RestartServer()
    {
        _trayIcon.ShowBalloonTip("Jellyfin", "正在重启服务...", ToolTipIcon.Info, 2000);
        await _serverManager.RestartAsync();
    }

    private void OpenSettings()
    {
        if (_settingsForm == null || _settingsForm.IsDisposed)
        {
            _settingsForm = _services.GetRequiredService<SettingsForm>();
            _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        }

        if (_settingsForm.WindowState == FormWindowState.Minimized)
        {
            _settingsForm.WindowState = FormWindowState.Normal;
        }

        _settingsForm.Show();
        _settingsForm.BringToFront();
    }

    private void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;

        _logger.LogInformation("Shutting down...");

        if (_options.MinimizeToTrayOnClose)
        {
            _trayIcon.ShowBalloonTip("Jellyfin", "正在退出...", ToolTipIcon.Info, 1000);
        }

        _serverManager.StopAsync().GetAwaiter().GetResult();
        _trayIcon.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _serverManager.OnServerStatusChanged -= OnServerStatusChanged;
            _serverManager.OnServerOutput -= OnServerOutput;

            _trayIcon.OnOpenWebInterface -= OpenWebInterface;
            _trayIcon.OnRestartServer -= RestartServer;
            _trayIcon.OnStopServer -= StopServer;
            _trayIcon.OnStartServer -= StartServer;
            _trayIcon.OnOpenSettings -= OpenSettings;
            _trayIcon.OnExit -= ExitApplication;

            _settingsForm?.Dispose();
            _uiForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}