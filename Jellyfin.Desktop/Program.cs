using System;
using System.Reflection;
using System.Windows.Forms;
using Jellyfin.Desktop.Configuration;
using Jellyfin.Desktop.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Desktop
{
    internal static class Program
    {
        // Marker class for logging
        private sealed class ProgramMarker { }

        [STAThread]
        private static void Main(string[] args)
        {
            var isService = args.Contains("--service") || args.Contains("-s");

            if (isService)
            {
                RunAsService(args);
            }
            else
            {
                RunAsTrayApp(args);
            }
        }

        private static void RunAsService(string[] args)
        {
            var host = CreateHostBuilder(args)
                .UseWindowsService()
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<ProgramMarker>>();
            logger.LogInformation("Starting Jellyfin Desktop as Windows Service v{Version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3));

            host.Run();
        }

        private static void RunAsTrayApp(string[] args)
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var host = CreateHostBuilder(args).Build();
            var logger = host.Services.GetRequiredService<ILogger<ProgramMarker>>();
            var singleInstance = host.Services.GetRequiredService<ISingleInstanceManager>();

            if (!singleInstance.IsFirstInstance)
            {
                _ = singleInstance.SignalFirstInstanceAsync(args);
                return;
            }

            try
            {
                logger.LogInformation("Starting Jellyfin Desktop v{Version}", Assembly.GetExecutingAssembly().GetName().Version?.ToString(3));

                var appContext = host.Services.GetRequiredService<TrayApplicationContext>();
                Application.Run(appContext);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Application terminated unexpectedly");
                MessageBox.Show($"启动失败: {ex.Message}", "Jellyfin Desktop", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(AppContext.BaseDirectory);
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables("JELLYFIN_DESKTOP_");
                    config.AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<DesktopOptions>(context.Configuration.GetSection("JellyfinDesktop"));
                    services.AddSingleton<TrayApplicationContext>();
                    services.AddSingleton<IServerProcessManager, ServerProcessManager>();
                    services.AddSingleton<ITrayIconService, TrayIconService>();
                    services.AddSingleton<IStartupManager, StartupManager>();
                    services.AddSingleton<ISingleInstanceManager, SingleInstanceManager>();
                    services.AddSingleton<SettingsForm>();
                    services.AddHostedService<DesktopHostedService>();
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                });
    }
}