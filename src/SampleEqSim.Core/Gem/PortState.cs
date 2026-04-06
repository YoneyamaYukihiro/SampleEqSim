namespace SampleEqSim.Core.Gem;

/// <summary>ロードポート状態 (SEMI E87)</summary>
public enum PortState
{
    OutOfService    = 0,
    InService       = 1,
    ReadyToLoad     = 2,
    ReadyToUnload   = 3,
    TransferBlocked = 4,
}

/// <summary>ポートアクセスモード (SEMI E87)</summary>
public enum PortAccessMode
{
    Manual = 0,
    Auto   = 1,
}
