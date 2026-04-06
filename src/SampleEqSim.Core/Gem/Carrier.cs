namespace SampleEqSim.Core.Gem;

/// <summary>キャリア定義 (SEMI E87)</summary>
public class Carrier
{
    public string       CarrierId    { get; }
    public uint         PortId       { get; set; }
    public CarrierState State        { get; set; } = CarrierState.WaitingForHost;
    public int          SlotCount    { get; }
    /// <summary>スロット番号 → ウェーハ/基板 ID (空文字 = 空スロット)</summary>
    public Dictionary<int, string> SlotMap { get; } = new();

    public Carrier(string carrierId, uint portId, int slotCount = 25)
    {
        CarrierId = carrierId;
        PortId    = portId;
        SlotCount = slotCount;
        for (int i = 1; i <= slotCount; i++)
            SlotMap[i] = "";
    }
}
