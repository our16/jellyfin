using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Desktop.Services;

public sealed class SingleInstanceManager : ISingleInstanceManager, IDisposable
{
    private const string PipeName = "JellyfinDesktopSingleInstance";
    private readonly ILogger<SingleInstanceManager> _logger;
    private NamedPipeServerStream? _pipeServer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _listenerTask;

    public bool IsFirstInstance { get; private set; }

    public SingleInstanceManager(ILogger<SingleInstanceManager> logger)
    {
        _logger = logger;
        IsFirstInstance = TryCreatePipeServer();
        if (IsFirstInstance)
        {
            StartListener();
        }
    }

    private bool TryCreatePipeServer()
    {
        try
        {
            _pipeServer = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void StartListener()
    {
        _listenerTask = Task.Run(async () =>
        {
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    await _pipeServer!.WaitForConnectionAsync(_cts.Token);
                    await HandleClientAsync(_pipeServer, _cts.Token);
                    _pipeServer.Disconnect();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Single instance listener error");
            }
        }, _cts.Token);
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            var buffer = new byte[1024];
            var bytesRead = await pipe.ReadAsync(buffer, ct);
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            _logger.LogInformation("Received message from second instance: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling client connection");
        }
    }

    public async Task SignalFirstInstanceAsync(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(1000);
            var message = string.Join(" ", args);
            var data = Encoding.UTF8.GetBytes(message);
            await client.WriteAsync(data, cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to signal first instance");
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listenerTask?.Wait(1000);
        _pipeServer?.Dispose();
        _cts.Dispose();
    }
}