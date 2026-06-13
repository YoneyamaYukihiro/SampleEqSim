using System.Text.Json.Nodes;
using SampleEqSim.Core.Config;
using Xunit;

namespace SampleEqSim.Tests;

/// <summary>
/// appsettings.json の secs4net 設定の読み書き (接続設定UI のバックエンド) のテスト。
/// </summary>
public class AppSettingsServiceTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"appsettings-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private const string SampleJson = """
    {
      "secs4net": {
        "DeviceId": 0,
        "IsActive": false,
        "IpAddress": "0.0.0.0",
        "Port": 5000,
        "T3Timeout": 45000,
        "LinkTestEnable": true
      },
      "Equipment": { "ModelName": "SampleEquipment", "SoftRev": "1.0.0" },
      "Logging": { "LogLevel": { "Default": "Information" } }
    }
    """;

    [Fact]
    public void Load_ReadsSecs4NetSection()
    {
        File.WriteAllText(_path, SampleJson);

        var s = AppSettingsService.Load(_path);

        Assert.False(s.IsActive);
        Assert.Equal("0.0.0.0", s.IpAddress);
        Assert.Equal(5000, s.Port);
        Assert.Equal((ushort)0, s.DeviceId);
    }

    [Fact]
    public void Save_UpdatesConnectionKeys_AndRoundTrips()
    {
        File.WriteAllText(_path, SampleJson);

        AppSettingsService.Save(new ConnectionSettings(true, "192.168.0.50", 7001, 12), _path);
        var s = AppSettingsService.Load(_path);

        Assert.True(s.IsActive);
        Assert.Equal("192.168.0.50", s.IpAddress);
        Assert.Equal(7001, s.Port);
        Assert.Equal((ushort)12, s.DeviceId);
    }

    [Fact]
    public void Save_PreservesOtherSections_AndOtherSecs4NetKeys()
    {
        File.WriteAllText(_path, SampleJson);

        AppSettingsService.Save(new ConnectionSettings(true, "10.0.0.1", 6000, 3), _path);

        var root = JsonNode.Parse(File.ReadAllText(_path))!.AsObject();

        // 他セクションが保持されていること
        Assert.Equal("SampleEquipment", root["Equipment"]!["ModelName"]!.GetValue<string>());
        Assert.Equal("Information", root["Logging"]!["LogLevel"]!["Default"]!.GetValue<string>());
        // secs4net 内の編集対象外キーが保持されていること
        Assert.Equal(45000, root["secs4net"]!["T3Timeout"]!.GetValue<int>());
        Assert.True(root["secs4net"]!["LinkTestEnable"]!.GetValue<bool>());
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var s = AppSettingsService.Load(_path); // ファイル無し

        Assert.False(s.IsActive);
        Assert.Equal("127.0.0.1", s.IpAddress);
        Assert.Equal(5000, s.Port);
    }
}
