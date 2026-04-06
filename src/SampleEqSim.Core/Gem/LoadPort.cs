namespace SampleEqSim.Core.Gem;

/// <summary>ロードポート定義 (SEMI E87)</summary>
public class LoadPort
{
    public uint           PortId     { get; }
    public string         PortName   { get; }
    public PortState      State      { get; set; } = PortState.InService;
    public PortAccessMode AccessMode { get; set; } = PortAccessMode.Auto;
    /// <summary>現在このポートに載っているキャリア ID (null = 空)</summary>
    public string?        CarrierId  { get; set; }

    public LoadPort(uint portId, string portName)
    {
        PortId   = portId;
        PortName = portName;
    }
}
