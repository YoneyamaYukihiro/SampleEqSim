namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM ステータス変数定義 (SEMI E30 §10.5)
/// </summary>
public class StatusVariable
{
    public uint VariableId { get; }
    public string VariableName { get; }
    public string Units { get; }
    public string Format { get; }

    private readonly Func<object> _valueGetter;

    public StatusVariable(uint variableId, string variableName, string format, Func<object> valueGetter, string units = "")
    {
        VariableId = variableId;
        VariableName = variableName;
        Format = format;
        Units = units;
        _valueGetter = valueGetter;
    }

    public object GetValue() => _valueGetter();
}
