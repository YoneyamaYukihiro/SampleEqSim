using System.IO;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Threading;
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

        _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
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

        try
        {
            await _host.StartAsync();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex) when (FindSocketException(ex) is { } se)
        {
            // Host は Active (接続側) のため通常 bind 競合は起きないが、
            // ネットワーク初期化失敗時に分かりやすく通知する。
            MessageBox.Show(
                $"ネットワーク初期化に失敗しました。\n\n{se.Message}",
                "起動エラー (HSMS)", MessageBoxButton.OK, MessageBoxImage.Error);
            WriteLog("HSMS 起動エラー", ex);
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
