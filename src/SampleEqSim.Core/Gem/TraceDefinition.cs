namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM トレースデータ収集定義 (SEMI E30 §10.9)
/// </summary>
public class TraceDefinition
{
    public uint TraceRequestId { get; }
    /// <summary>サンプリング期間 (秒)</summary>
    public uint SamplingPeriod { get; set; }
    /// <summary>サンプル数 (0=無限)</summary>
    public uint TotalSamples { get; set; }
    /// <summary>対象変数IDリスト</summary>
    public List<uint> VariableIds { get; }
    /// <summary>収集済みサンプル数</summary>
    public uint CollectedSamples { get; set; } = 0;
    public bool IsActive { get; set; } = false;

    public TraceDefinition(uint traceRequestId, uint samplingPeriod, uint totalSamples, IEnumerable<uint> variableIds)
    {
        TraceRequestId = traceRequestId;
        SamplingPeriod = samplingPeriod;
        TotalSamples = totalSamples;
        VariableIds = new List<uint>(variableIds);
    }
}
