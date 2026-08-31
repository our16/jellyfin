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
    private readonly CancellationTokenSource _outputCts = new();
    private int _restartAttempts;
    private System.Threading.Timer? _healthCheckTimer;

    public event Action<bool>? OnServerStatusChanged;
    public event Action<string>? OnServerOutput;

    public bool IsRunning => _serverProcess != null && !_serverProcess.HasExited;
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

            _serverProcess.Exited += (_, _) =>
            {
                _logger.LogInformation("Server process exited with code {Code}", _serverProcess?.ExitCode);
                OnServerStatusChanged?.Invoke(false);
                OnServerOutput?.Invoke($"服务进程已退出 (代码: {_serverProcess?.ExitCode})");

                if (_options.RestartOnCrash && _restartAttempts < _options.MaxRestartAttempts)
                {
                    _restartAttempts++;
                    _logger.LogInformation("Scheduling restart attempt {Attempt}/{Max}", _restartAttempts, _options.MaxRestartAttempts);
                    Task.Delay(TimeSpan.FromSeconds(_options.RestartDelaySeconds))
                        .ContinueWith(_ => RestartAsync().ConfigureAwait(false));
                }
            };

            var started = _serverProcess.Start();
            if (!started)
            {
                _logger.LogError("Failed to start server process");
                return false;
            }

            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            _restartAttempts = 0;

            _healthCheckTimer = new System.Threading.Timer(HealthCheckCallback, null,
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds),
                TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds));

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
        await _processLock.WaitAsync(cancellationToken);
        try
        {
            _healthCheckTimer?.Dispose(); // Timer.DisposeAsync not available in .NET 8
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

    private void HealthCheckCallback(object? state)
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Health check failed: server process not running");
            OnServerStatusChanged?.Invoke(false);
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
        _healthCheckTimer?.Dispose();
        _outputCts.Cancel();
        _outputCts.Dispose();
        _processLock.Dispose();

        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try { _serverProcess.Kill(); } catch { }
            _serverProcess.Dispose();
        }
    }
}