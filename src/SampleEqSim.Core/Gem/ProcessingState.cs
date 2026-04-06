namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM Equipment Processing State Model (SEMI E30 §8.5)
/// </summary>
public enum ProcessingState
{
    /// <summary>初期化中</summary>
    Init = 0,

    /// <summary>アイドル (処理なし)</summary>
    Idle = 1,

    /// <summary>セットアップ中</summary>
    Setup = 2,

    /// <summary>実行準備完了</summary>
    Ready = 3,

    /// <summary>実行中</summary>
    Executing = 4,

    /// <summary>一時停止中</summary>
    Pause = 5,
}
