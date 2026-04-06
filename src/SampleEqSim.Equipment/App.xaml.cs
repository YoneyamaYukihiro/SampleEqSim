using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secs4Net;
using SampleEqSim.Core.Gem;
using SampleEqSim.Core.Secs;
using SampleEqSim.Equipment.ViewModels;
using SampleEqSim.Equipment.Views;

namespace SampleEqSim.Equipment;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<ISecsGem, NoopSecsGem>();

                // GEM Model
                services.AddSingleton<GemEquipmentModel>(sp =>
                {
                    var secsGem = sp.GetRequiredService<ISecsGem>();
                    var logger = sp.GetRequiredService<ILogger<GemEquipmentModel>>();
                    var config = sp.GetRequiredService<IConfiguration>();

                    var model = new GemEquipmentModel(secsGem, logger)
                    {
                        ModelName = config["Equipment:ModelName"] ?? "SampleEquipment",
                        SoftRev = config["Equipment:SoftRev"] ?? "1.0.0",
                    };
                    return model;
                });

                // ViewModel & View
                services.AddSingleton<EquipmentViewModel>();
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
