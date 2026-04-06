namespace SampleEqSim.Core.Gem;

/// <summary>キャリア状態 (SEMI E87)</summary>
public enum CarrierState
{
    Unknown         = 0,
    WaitingForHost  = 1,
    InProcess       = 2,
    CarrierComplete = 3,
    TransferBlocked = 4,
}
