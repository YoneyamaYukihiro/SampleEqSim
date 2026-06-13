using System.Text.Json;
using System.Text.Json.Nodes;

namespace SampleEqSim.Core.Config;

/// <summary>HSMS 接続設定 (UI から編集可能な項目)。</summary>
public sealed record ConnectionSettings(bool IsActive, string IpAddress, int Port, ushort DeviceId);

/// <summary>
/// 実行中アプリの <c>appsettings.json</c> (exe と同じフォルダ) の <c>secs4net</c> セクションを
/// 読み書きするヘルパー。他のセクション (Equipment / Logging 等) は保持する。
/// 変更は次回起動時に <see cref="Microsoft.Extensions.DependencyInjection"/> 経由で反映される。
/// </summary>
public static class AppSettingsService
{
    /// <summary>編集対象の appsettings.json (出力フォルダにコピーされた実体)。</summary>
    public static string SettingsPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>現在の接続設定を読み込む。ファイルやキーが無い場合は既定値を返す。</summary>
    public static ConnectionSettings Load() => Load(SettingsPath);

    /// <summary>接続設定を保存する。secs4net セクション内の該当キーのみ更新する。</summary>
    public static void Save(ConnectionSettings settings) => Save(settings, SettingsPath);

    /// <summary>指定パスから読み込む (テスト用オーバーロード)。</summary>
    public static ConnectionSettings Load(string path)
    {
        var secs = ReadRoot(path)?["secs4net"];
        return new ConnectionSettings(
            IsActive:  GetBool(secs, "IsActive", false),
            IpAddress: secs?["IpAddress"]?.GetValue<string>() ?? "127.0.0.1",
            Port:      GetInt(secs, "Port", 5000),
            DeviceId:  (ushort)GetInt(secs, "DeviceId", 0));
    }

    /// <summary>
    /// 指定パスへ保存する (テスト用オーバーロード)。secs4net セクション内の該当キーのみ更新し、
    /// 他のセクション (Equipment / Logging 等) と secs4net 内の他キー (T3〜T8 等) は保持する。
    /// </summary>
    public static void Save(ConnectionSettings settings, string path)
    {
        var root = ReadRoot(path) ?? new JsonObject();
        if (root["secs4net"] is not JsonObject secs)
        {
            secs = new JsonObject();
            root["secs4net"] = secs;
        }
        secs["IsActive"]  = settings.IsActive;
        secs["IpAddress"] = settings.IpAddress;
        secs["Port"]      = settings.Port;
        secs["DeviceId"]  = (int)settings.DeviceId;

        File.WriteAllText(path,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static JsonObject? ReadRoot(string path)
    {
        if (!File.Exists(path)) return null;
        return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
    }

    private static int GetInt(JsonNode? parent, string key, int fallback)
        => parent?[key] is JsonValue v && v.TryGetValue<int>(out var i) ? i : fallback;

    private static bool GetBool(JsonNode? parent, string key, bool fallback)
        => parent?[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
}
