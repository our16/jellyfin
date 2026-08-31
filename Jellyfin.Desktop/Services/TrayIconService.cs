using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Jellyfin.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Desktop.Services;

public sealed class TrayIconService : ITrayIconService, IDisposable
{
    private readonly ILogger<TrayIconService> _logger;
    private readonly DesktopOptions _options;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private Icon? _defaultIcon;
    private Icon? _runningIcon;
    private Icon? _stoppedIcon;

    public event Action? OnOpenWebInterface;
    public event Action? OnRestartServer;
    public event Action? OnStopServer;
    public event Action? OnStartServer;
    public event Action? OnOpenSettings;
    public event Action? OnExit;

    public bool IsServerRunning { get; set; }
    public string ServerUrl { get; set; } = "http://localhost:8096";

    public TrayIconService(ILogger<TrayIconService> logger, IOptions<DesktopOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public void Initialize()
    {
        LoadIcons();
        CreateContextMenu();
        CreateNotifyIcon();

        if (_options.ShowStartupBalloon)
        {
            ShowBalloonTip("Jellyfin Desktop", "Jellyfin 正在后台运行", ToolTipIcon.Info, 5000);
        }

        UpdateTrayIcon();
        _logger.LogInformation("Tray icon initialized");
    }

    private void LoadIcons()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Jellyfin.Desktop.Jellyfin.Desktop.ico");
            if (stream != null)
            {
                _defaultIcon = new Icon(stream);
                _runningIcon = _defaultIcon;
                _stoppedIcon = CreateGrayIcon(_defaultIcon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load custom icon, using system default");
            _defaultIcon = SystemIcons.Application;
            _runningIcon = SystemIcons.Application;
            _stoppedIcon = SystemIcons.Warning;
        }
    }

    private static Icon CreateGrayIcon(Icon source)
    {
        var bitmap = source.ToBitmap();
        for (int x = 0; x < bitmap.Width; x++)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                var color = bitmap.GetPixel(x, y);
                var gray = (int)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);
                bitmap.SetPixel(x, y, Color.FromArgb(color.A, gray, gray, gray));
            }
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        var openWebItem = new ToolStripMenuItem("打开 Web 界面", null, (_, _) => OnOpenWebInterface?.Invoke())
        {
            Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
        };
        _contextMenu.Items.Add(openWebItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var serverStatusItem = new ToolStripMenuItem("服务状态: 停止中", (Image?)null, (EventHandler?)null)
        {
            Enabled = false,
            Name = "ServerStatusItem"
        };
        _contextMenu.Items.Add(serverStatusItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var startItem = new ToolStripMenuItem("启动服务", null, (_, _) => OnStartServer?.Invoke())
        {
            Name = "StartServerItem"
        };
        _contextMenu.Items.Add(startItem);

        var stopItem = new ToolStripMenuItem("停止服务", null, (_, _) => OnStopServer?.Invoke())
        {
            Name = "StopServerItem"
        };
        _contextMenu.Items.Add(stopItem);

        var restartItem = new ToolStripMenuItem("重启服务", null, (_, _) => OnRestartServer?.Invoke())
        {
            Name = "RestartServerItem"
        };
        _contextMenu.Items.Add(restartItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var settingsItem = new ToolStripMenuItem("设置", null, (_, _) => OnOpenSettings?.Invoke());
        _contextMenu.Items.Add(settingsItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("退出", null, (_, _) => OnExit?.Invoke());
        _contextMenu.Items.Add(exitItem);

        _contextMenu.Opening += (_, _) => UpdateMenuItems();
    }

    private void UpdateMenuItems()
    {
        if (_contextMenu == null) return;

        var statusItem = _contextMenu.Items["ServerStatusItem"] as ToolStripMenuItem;
        var startItem = _contextMenu.Items["StartServerItem"] as ToolStripMenuItem;
        var stopItem = _contextMenu.Items["StopServerItem"] as ToolStripMenuItem;
        var restartItem = _contextMenu.Items["RestartServerItem"] as ToolStripMenuItem;

        if (statusItem != null)
        {
            statusItem.Text = IsServerRunning ? "服务状态: 运行中" : "服务状态: 已停止";
        }

        if (startItem != null) startItem.Enabled = !IsServerRunning;
        if (stopItem != null) stopItem.Enabled = IsServerRunning;
        if (restartItem != null) restartItem.Enabled = IsServerRunning;
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = _defaultIcon,
            Text = "Jellyfin Desktop",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (_, _) => OnOpenWebInterface?.Invoke();
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _contextMenu?.Show(Cursor.Position);
            }
        };
    }

    public void ShowBalloonTip(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, message, icon);
    }

    public void UpdateStatus(string status, Icon? icon = null)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Text = $"Jellyfin Desktop - {status}";
            _notifyIcon.Icon = icon ?? (IsServerRunning ? _runningIcon : _stoppedIcon);
        }
        UpdateMenuItems();
    }

    private void UpdateTrayIcon()
    {
        UpdateStatus(IsServerRunning ? "运行中" : "已停止", IsServerRunning ? _runningIcon : _stoppedIcon);
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
        _defaultIcon?.Dispose();
        _stoppedIcon?.Dispose();
    }
}