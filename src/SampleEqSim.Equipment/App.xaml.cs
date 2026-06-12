using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secs4Net;
using SampleEqSim.Core.Gem;
using SampleEqSim.Equipment.ViewModels;
using SampleEqSim.Equipment.Views;

namespace SampleEqSim.Equipment;

public partial class App : Application
{
    private IHost? _host;

    public App()
    {
        // ダブルクリック起動ではコンソールが無く、未処理例外が出ても画面に何も出ずに
        // アプリが黙って終了する。あらゆる起動時/実行時例外をログ + ダイアログで可視化する。
        DispatcherUnhandledException += (_, args) =>
        {
            ReportFatal("UI スレッド例外", args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            ReportFatal("未処理例外", args.ExceptionObject as Exception);
    }

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
                // SECS/GEM (Passive = Equipment side)
                services.AddSecs4Net<DeviceLogger>(context.Configuration);

                // GemEquipmentModel: シングルトン + IHostedService として両方登録
                services.AddSingleton<GemEquipmentModel>(sp =>
                {
                    var secsGem    = sp.GetRequiredService<ISecsGem>();
                    var connection = sp.GetRequiredService<ISecsConnection>();
                    var logger     = sp.GetRequiredService<ILogger<GemEquipmentModel>>();
                    var config     = sp.GetRequiredService<IConfiguration>();
                    return new GemEquipmentModel(secsGem, connection, logger)
                    {
                        ModelName = config["Equipment:ModelName"] ?? "SampleEquipment",
                        SoftRev   = config["Equipment:SoftRev"]   ?? "1.0.0",
                    };
                });
                // 同インスタンスを IHostedService として登録 (メッセージループ起動)
                services.AddHostedService(sp => sp.GetRequiredService<GemEquipmentModel>());

                // ViewModel & View
                services.AddSingleton<EquipmentViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .ConfigureLogging(logging =>
            {
                logging.AddConsole();
            })
            .Build();

        try
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex) when (FindSocketException(ex) is { } se)
        {
            // ポート使用中 (WSAEADDRINUSE=10048) を分かりやすく通知。
            // HsmsConnection はコンストラクタで Socket.Bind するため、ここで補足される。
            var port = _host.Services.GetRequiredService<IConfiguration>()
                .GetValue<int?>("secs4net:Port") ?? 5000;
            var detail = se.SocketErrorCode == SocketError.AddressAlreadyInUse
                ? $"ポート {port} は既に使用されています。\n\n" +
                  "別の Equipment シミュレーターが起動中の可能性があります。\n" +
                  "残っているプロセスを終了してから再度起動してください。\n" +
                  "(タスクマネージャーで SampleEqSim.Equipment.exe を終了、\n" +
                  $" または コマンドで netstat -ano | findstr :{port})"
                : $"ネットワーク初期化に失敗しました。\n\n{se.Message}";
            WriteLog("HSMS 起動エラー", ex);
            MessageBox.Show(detail, "起動エラー (HSMS)", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
        catch (Exception ex)
        {
            // SocketException 以外の起動失敗（DI 解決失敗・XAML/リソース読込エラー等）。
            ReportFatal("起動処理", ex);
            Shutdown(1);
        }
    }

    /// <summary>例外チェーンを辿って最初の <see cref="SocketException"/> を返す。</summary>
    private static SocketException? FindSocketException(Exception? ex)
    {
        for (; ex != null; ex = ex.InnerException)
            if (ex is SocketException se)
                return se;
        return null;
    }

    /// <summary>
    /// 致命的例外をログファイル (exe と同じフォルダの startup-error.log) に追記し、
    /// ダイアログで通知する。ダブルクリック起動でも原因が分かるようにする。
    /// </summary>
    private static void ReportFatal(string context, Exception? ex)
    {
        var logPath = WriteLog(context, ex);
        var summary = ex?.Message ?? "(不明な例外)";
        MessageBox.Show(
            $"{context}でエラーが発生しました。\n\n{summary}\n\n詳細は次のログを確認してください:\n{logPath}",
            "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>例外を exe と同じフォルダの startup-error.log に追記し、パスを返す。</summary>
    private static string WriteLog(string context, Exception? ex)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
        try
        {
            File.AppendAllText(logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}" +
                $"{ex?.ToString() ?? "(不明な例外)"}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* ログ書込失敗は無視 */ }
        return logPath;
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
