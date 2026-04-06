namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM リミット監視定義 (SEMI E30 §10.12)
/// </summary>
public class VariableLimitAttribute
{
    public uint VariableId { get; }
    public List<LimitPair> Limits { get; } = new();

    public VariableLimitAttribute(uint variableId)
    {
        VariableId = variableId;
    }
}

public class LimitPair
{
    public uint LimitId { get; set; }
    public object? UpperLimit { get; set; }
    public object? LowerLimit { get; set; }
    public uint UpperCollectionEventId { get; set; }
    public uint LowerCollectionEventId { get; set; }
}
