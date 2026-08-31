using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Desktop.Configuration;
using Jellyfin.Desktop.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Desktop;

public sealed class DesktopHostedService : BackgroundService
{
    private readonly ILogger<DesktopHostedService> _logger;
    private readonly IServerProcessManager _serverManager;
    private readonly DesktopOptions _options;

    public DesktopHostedService(
        ILogger<DesktopHostedService> logger,
        IServerProcessManager serverManager,
        IOptions<DesktopOptions> options)
    {
        _logger = logger;
        _serverManager = serverManager;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Desktop hosted service started");

        if (_options.AutoStartServer)
        {
            _logger.LogInformation("Auto-starting server...");
            await _serverManager.StartAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("Desktop hosted service stopping");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping desktop hosted service");
        await _serverManager.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }
}