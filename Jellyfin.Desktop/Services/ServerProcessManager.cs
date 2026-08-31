#pragma warning disable CA1849

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Desktop.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Desktop.Services;

public interface IServerProcessManager
{
    event Action<bool>? OnServerStatusChanged;
    event Action<string>? OnServerOutput;
    bool IsRunning { get; }
    int? ProcessId { get; }
    Task<bool> StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RestartAsync(CancellationToken cancellationToken = default);
    string GetServerUrl();
}

public sealed class ServerProcessManager : IServerProcessManager, IDisposable
{
    private readonly ILogger<ServerProcessManager> _logger;
    private readonly DesktopOptions _options;
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private Process? _serverProcess;
    private int _restartAttempts;
    private volatile bool _isStopping;
    private System.Threading.Timer? _healthCheckTimer;
    private CancellationTokenSource? _restartCts;

    public event Action<bool>? OnServerStatusChanged;
    public event Action<string>? OnServerOutput;

    public bool IsRunning
    {
        get
        {
            var p = _serverProcess;
            return p != null && !p.HasExited;
        }
    }

    public int? ProcessId => _serverProcess?.Id;

    public ServerProcessManager(ILogger<ServerProcessManager> logger, IOptions<DesktopOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> StartAsync(CancellationToken cancellationToken = default)
    {
        await _processLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
            {
                _logger.LogWarning("Server is already running (PID: {Pid})", _serverProcess!.Id);
                return true;
            }

            var exePath = FindServerExecutable();
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                _logger.LogError("Server executable not found: {Path}", exePath);
                OnServerOutput?.Invoke($"错误: 找不到服务器程序 {exePath}");
                return false;
            }

            _logger.LogInformation("Starting server: {Exe} {Args}", exePath, _options.ServerArguments);

            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = _options.ServerArguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            _serverProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OnServerOutput?.Invoke(e.Data);
                }
            };

            _serverProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    OnServerOutput?.Invoke($"[ERROR] {e.Data}");
                }
            };

            _serverProcess.Exited += OnServerExited;

            var started = _serverProcess.Start();
            if (!started)
            {
                _logger.LogError("Failed to start server process");
                return false;
            }

            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            OnServerStatusChanged?.Invoke(true);
            OnServerOutput?.Invoke($"服务已启动 (PID: {_serverProcess.Id})");
            _logger.LogInformation("Server started successfully (PID: {Pid})", _serverProcess.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting server");
            OnServerOutput?.Invoke($"启动失败: {ex.Message}");
            return false;
        }
        finally
        {
            _processLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // Cancel any pending restart
        _restartCts?.Cancel();
        _restartCts = null;

        await _processLock.WaitAsync(cancellationToken);
        try
        {
            _isStopping = true;
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = null;

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                _logger.LogInformation("Server is not running");
                return;
            }

            _logger.LogInformation("Stopping server (PID: {Pid})", _serverProcess.Id);
            OnServerOutput?.Invoke("正在停止服务...");

            _serverProcess.CancelErrorRead();
            _serverProcess.CancelOutputRead();

            if (!_serverProcess.HasExited)
            {
                _serverProcess.Kill(true);
                await _serverProcess.WaitForExitAsync(cancellationToken);
            }

            _serverProcess.Exited -= OnServerExited;
            _serverProcess.Dispose();
            _serverProcess = null;

            OnServerStatusChanged?.Invoke(false);
            OnServerOutput?.Invoke("服务已停止");
            _logger.LogInformation("Server stopped");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Stop operation cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping server");
            OnServerOutput?.Invoke($"停止失败: {ex.Message}");
        }
        finally
        {
            _isStopping = false;
            _processLock.Release();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await StopAsync(cancellationToken);
        await Task.Delay(1000, cancellationToken);
        await StartAsync(cancellationToken);
    }

    public string GetServerUrl()
    {
        return "http://localhost:8096";
    }

    private void OnServerExited(object? sender, EventArgs e)
    {
        if (_isStopping)
        {
            _logger.LogInformation("Server stopped intentionally, skipping restart");
            return;
        }

        var exitCode = _serverProcess?.ExitCode ?? -1;
        _logger.LogInformation("Server process exited with code {Code}", exitCode);
        OnServerStatusChanged?.Invoke(false);
        OnServerOutput?.Invoke($"服务进程已退出 (代码: {exitCode})");

        if (_options.RestartOnCrash && exitCode != 0)
        {
            _restartAttempts++;
            if (_restartAttempts > _options.MaxRestartAttempts)
            {
                _logger.LogWarning("Max restart attempts ({Max}) reached, not restarting", _options.MaxRestartAttempts);
                OnServerOutput?.Invoke($"已达到最大重启次数 ({_options.MaxRestartAttempts})，不再重启");
                return;
            }

            _logger.LogInformation("Scheduling restart attempt {Attempt}/{Max} in {Delay}s",
                _restartAttempts, _options.MaxRestartAttempts, _options.RestartDelaySeconds);
            OnServerOutput?.Invoke($"将在 {_options.RestartDelaySeconds} 秒后重启 (第 {_restartAttempts}/{_options.MaxRestartAttempts} 次)...");

            _restartCts?.Cancel();
            _restartCts = new CancellationTokenSource();
            var ct = _restartCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.RestartDelaySeconds), ct);
                    ct.ThrowIfCancellationRequested();
                    await RestartAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Restart cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during restart");
                }
            });
        }
        else if (exitCode == 0)
        {
            _logger.LogInformation("Server exited cleanly (code 0), not restarting");
            _restartAttempts = 0;
        }
    }

    private void HealthCheckCallback(object? state)
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Health check failed: server process not running");
            OnServerStatusChanged?.Invoke(false);
        }
        else
        {
            // Server is healthy, reset restart counter
            _restartAttempts = 0;
        }
    }

    private string? FindServerExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, _options.ServerExecutableName),
            Path.Combine(baseDir, "Jellyfin.Server", _options.ServerExecutableName),
            Path.Combine(baseDir, "..", "Jellyfin.Server", _options.ServerExecutableName),
            Path.Combine(baseDir, "..", "..", "Jellyfin.Server", _options.ServerExecutableName),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    public void Dispose()
    {
        _restartCts?.Cancel();
        _restartCts?.Dispose();
        _healthCheckTimer?.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try { _serverProcess.Kill(); } catch { }
        }
        _serverProcess?.Dispose();
        _processLock.Dispose();
    }
}