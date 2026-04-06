namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM データ変数 (DVID: Data Variable) (SEMI E30 §10.5)
/// トレースデータ収集に使用される変数
/// </summary>
public class DataVariable
{
    public uint VariableId { get; }
    public string VariableName { get; }
    public string Format { get; }
    public string Units { get; }

    private readonly Func<object> _valueGetter;

    public DataVariable(uint variableId, string variableName, string format, Func<object> valueGetter, string units = "")
    {
        VariableId = variableId;
        VariableName = variableName;
        Format = format;
        Units = units;
        _valueGetter = valueGetter;
    }

    public object GetValue() => _valueGetter();
}
