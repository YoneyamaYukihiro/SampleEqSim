namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM Control State Model (SEMI E30 §8.4)
/// </summary>
public enum ControlState
{
    /// <summary>装置オフライン</summary>
    EquipmentOffline = 0,

    /// <summary>オンライン試行中</summary>
    AttemptOnline = 1,

    /// <summary>ホスト主導オフライン</summary>
    HostOffline = 2,

    /// <summary>オンライン (ローカル制御)</summary>
    OnlineLocal = 3,

    /// <summary>オンライン (リモート制御)</summary>
    OnlineRemote = 4,
}
