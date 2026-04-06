namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM Communication State Model (SEMI E30 §8.3)
/// </summary>
public enum CommunicationState
{
    /// <summary>通信無効状態</summary>
    Disabled = 0,

    /// <summary>通信中でない (サブ状態: ホストからのCR待ち)</summary>
    WaitCrFromHost = 1,

    /// <summary>通信中でない (サブ状態: 遅延待ち)</summary>
    WaitDelay = 2,

    /// <summary>通信中でない (サブ状態: CR送信待ち)</summary>
    WaitCr = 3,

    /// <summary>通信中</summary>
    Communicating = 4,
}
