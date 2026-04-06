namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM 装置定数定義 (SEMI E30 §10.7)
/// </summary>
public class EquipmentConstant
{
    public uint ConstantId { get; }
    public string ConstantName { get; }
    public string Format { get; }
    public string Units { get; }
    public object DefaultValue { get; }
    public object MinValue { get; }
    public object MaxValue { get; }
    public object CurrentValue { get; set; }

    public EquipmentConstant(
        uint constantId,
        string constantName,
        string format,
        object defaultValue,
        object minValue,
        object maxValue,
        string units = "")
    {
        ConstantId = constantId;
        ConstantName = constantName;
        Format = format;
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
        CurrentValue = defaultValue;
        Units = units;
    }
}
