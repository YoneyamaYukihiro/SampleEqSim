using System.Diagnostics;

namespace SampleEqSim.Core.Config;

/// <summary>
/// 接続設定の入力検証・表示文言・アプリ再起動のための共通ユーティリティ。
/// Equipment / Host 両 ViewModel から利用する。
/// </summary>
public static class ConnectionSettingsUtil
{
    /// <summary>再起動した子プロセスに渡す引数 (起動側でポート解放待ちに使う)。</summary>
    public const string RestartArg = "--restart";

    /// <summary>UI 入力値を検証して <see cref="ConnectionSettings"/> を生成する。</summary>
    public static bool TryBuild(
        bool isActive, string? ipAddress, string? portText, string? deviceIdText,
        out ConnectionSettings settings, out string error)
    {
        settings = null!;
        error = "";

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            error = "IP アドレスを入力してください。\n(Passive で全インターフェース待受なら 0.0.0.0)";
            return false;
        }
        if (!int.TryParse(portText, out var port) || port < 1 || port > 65535)
        {
            error = "ポート番号は 1〜65535 の整数で入力してください。";
            return false;
        }
        if (!ushort.TryParse(deviceIdText, out var deviceId))
        {
            error = "DeviceId は 0〜65535 の整数で入力してください。";
            return false;
        }

        settings = new ConnectionSettings(isActive, ipAddress.Trim(), port, deviceId);
        return true;
    }

    /// <summary>ヘッダー等に表示する1行サマリ。</summary>
    public static string Summarize(ConnectionSettings s)
        => s.IsActive
            ? $"Active → {s.IpAddress}:{s.Port}  (DeviceId={s.DeviceId})"
            : $"Passive / Port {s.Port}  (DeviceId={s.DeviceId})";

    /// <summary>保存・再起動確認ダイアログの本文。</summary>
    public static string ConfirmMessage(ConnectionSettings s)
        => "以下の接続設定を保存し、アプリを再起動して反映します。\n\n" +
           $"モード   : {(s.IsActive ? "Active (自分から接続)" : "Passive (待受)")}\n" +
           $"IP       : {s.IpAddress}\n" +
           $"ポート   : {s.Port}\n" +
           $"DeviceId : {s.DeviceId}\n\n" +
           "よろしいですか？";

    /// <summary>
    /// 現在の exe を <see cref="RestartArg"/> 付きで新規起動する。
    /// 呼び出し側は本メソッドの後に自プロセスを終了させること
    /// (WPF なら <c>Application.Current.Shutdown()</c>)。
    /// </summary>
    public static void StartNewInstance()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe)) return;
        Process.Start(new ProcessStartInfo(exe, RestartArg)
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
        });
    }
}
