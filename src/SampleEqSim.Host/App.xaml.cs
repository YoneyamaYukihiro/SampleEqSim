using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secs4Net;
using SampleEqSim.Host.Services;
using SampleEqSim.Host.ViewModels;
using SampleEqSim.Host.Views;

namespace SampleEqSim.Host;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // SECS/GEM (Active = Host side)
                services.AddSecs4Net<DeviceLogger>(context.Configuration);

                // HostGemService: メッセージループ + 接続状態管理
                // シングルトンとして登録し、IHostedService にも追加
                services.AddSingleton<HostGemService>();
                services.AddHostedService(sp => sp.GetRequiredService<HostGemService>());

                // ViewModel & View
                services.AddSingleton<HostViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
