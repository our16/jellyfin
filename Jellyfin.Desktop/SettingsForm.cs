using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Jellyfin.Desktop.Configuration;
using Jellyfin.Desktop.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Desktop;

public sealed class SettingsForm : Form
{
    private readonly ILogger<SettingsForm> _logger;
    private readonly IOptions<DesktopOptions> _options;
    private readonly IStartupManager _startupManager;
    private readonly IServerProcessManager _serverManager;

    private CheckBox _chkStartAtLogin = null!;
    private CheckBox _chkAutoStartServer = null!;
    private CheckBox _chkMinimizeToTray = null!;
    private CheckBox _chkStartMinimized = null!;
    private TextBox _txtServerArgs = null!;
    private TextBox _txtServerUrl = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;
    private Button _btnOpenLog = null!;
    private Button _btnOpenDataDir = null!;

    public SettingsForm(
        ILogger<SettingsForm> logger,
        IOptions<DesktopOptions> options,
        IStartupManager startupManager,
        IServerProcessManager serverManager)
    {
        _logger = logger;
        _options = options;
        _startupManager = startupManager;
        _serverManager = serverManager;

        InitializeComponent();
        LoadSettings();
    }

    private void InitializeComponent()
    {
        Text = "Jellyfin Desktop 设置";
        Size = new Size(500, 450);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        ShowInTaskbar = false;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 8,
            AutoScroll = true
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // General Group
        var grpGeneral = CreateGroupBox("常规设置", new Control[]
        {
            (_chkStartAtLogin = new CheckBox { Text = "开机自动启动", AutoSize = true }),
            (_chkStartMinimized = new CheckBox { Text = "启动时最小化到托盘", AutoSize = true }),
            (_chkMinimizeToTray = new CheckBox { Text = "关闭窗口时最小化到托盘", AutoSize = true })
        });
        panel.Controls.Add(grpGeneral, 0, 0);

        // Server Group
        var grpServer = CreateGroupBox("服务设置", new Control[]
        {
            (_chkAutoStartServer = new CheckBox { Text = "启动时自动启动服务", AutoSize = true }),
            new Label { Text = "服务启动参数:", AutoSize = true },
            (_txtServerArgs = new TextBox { Dock = DockStyle.Top, Height = 60, Multiline = true, ScrollBars = ScrollBars.Vertical }),
            new Label { Text = "服务地址 (只读):", AutoSize = true },
            (_txtServerUrl = new TextBox { Dock = DockStyle.Top, ReadOnly = true, BackColor = SystemColors.Control })
        });
        panel.Controls.Add(grpServer, 0, 1);

        // Actions Group
        var grpActions = CreateGroupBox("快捷操作", new Control[]
        {
            CreateButtonRow("打开日志文件夹", _btnOpenLog = new Button { Text = "打开日志", AutoSize = true }),
            CreateButtonRow("打开数据文件夹", _btnOpenDataDir = new Button { Text = "打开数据", AutoSize = true })
        });
        panel.Controls.Add(grpActions, 0, 2);

        // Status Group
        var grpStatus = CreateGroupBox("服务状态", new Control[]
        {
            new Label { Text = $"运行状态: {(_serverManager.IsRunning ? "运行中" : "已停止")}", AutoSize = true, Name = "LblStatus" },
            new Label { Text = $"进程 ID: {(_serverManager.ProcessId?.ToString(CultureInfo.InvariantCulture) ?? "N/A")}", AutoSize = true, Name = "LblPid" },
            new Label { Text = $"地址: {_serverManager.GetServerUrl()}", AutoSize = true, Name = "LblUrl" }
        });
        panel.Controls.Add(grpStatus, 0, 3);

        // Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };
        _btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        _btnSave = new Button { Text = "保存", DialogResult = DialogResult.OK, AutoSize = true };
        _btnSave.Click += BtnSave_Click;
        _btnCancel.Click += (_, _) => Close();
        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);
        panel.Controls.Add(btnPanel, 0, 7);

        _btnOpenLog.Click += (_, _) => OpenFolder(GetLogDirectory());
        _btnOpenDataDir.Click += (_, _) => OpenFolder(GetDataDirectory());

        Controls.Add(panel);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private static GroupBox CreateGroupBox(string title, Control[] controls)
    {
        var grp = new GroupBox { Text = title, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(10) };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        foreach (var ctrl in controls)
        {
            layout.Controls.Add(ctrl);
        }
        grp.Controls.Add(layout);
        return grp;
    }

    private static Control CreateButtonRow(string label, Button button)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        panel.Controls.Add(new Label { Text = label, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Width = 120 });
        panel.Controls.Add(button);
        return panel;
    }

    private void LoadSettings()
    {
        _chkStartAtLogin.Checked = _options.Value.StartAtLogin;
        _chkStartMinimized.Checked = _options.Value.StartMinimized;
        _chkMinimizeToTray.Checked = _options.Value.MinimizeToTrayOnClose;
        _chkAutoStartServer.Checked = _options.Value.AutoStartServer;
        _txtServerArgs.Text = _options.Value.ServerArguments;
        _txtServerUrl.Text = _serverManager.GetServerUrl();
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var json = File.ReadAllText(configPath);

            // Simple JSON update - in production, use a proper JSON library
            json = UpdateJsonValue(json, "StartAtLogin", _chkStartAtLogin.Checked);
            json = UpdateJsonValue(json, "StartMinimized", _chkStartMinimized.Checked);
            json = UpdateJsonValue(json, "MinimizeToTrayOnClose", _chkMinimizeToTray.Checked);
            json = UpdateJsonValue(json, "AutoStartServer", _chkAutoStartServer.Checked);
            json = UpdateJsonValue(json, "ServerArguments", _txtServerArgs.Text);

            File.WriteAllText(configPath, json);

            // Apply startup setting immediately
            if (_chkStartAtLogin.Checked)
                _startupManager.Enable();
            else
                _startupManager.Disable();

            _logger.LogInformation("Settings saved");
            MessageBox.Show("设置已保存，部分设置需要重启应用生效", "Jellyfin Desktop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string UpdateJsonValue(string json, string key, object value)
    {
        var pattern = $"\"{key}\"\\s*:\\s*";
        var start = json.IndexOf(pattern, StringComparison.Ordinal);
        if (start < 0) return json;
        start += pattern.Length;
        var end = json.IndexOfAny([',', '}'], start);
        if (end < 0) end = json.Length;
        var newValue = value is bool b ? b.ToString().ToLowerInvariant() : $"\"{value}\"";
        return json.Remove(start, end - start).Insert(start, newValue);
    }

    private void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show($"文件夹不存在: {path}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open folder");
            MessageBox.Show($"打开失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string GetLogDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "logs");
    }

    private static string GetDataDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "data");
    }
}