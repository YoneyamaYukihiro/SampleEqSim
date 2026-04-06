namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM レポート定義 (SEMI E30 §10.4 Dynamic Event Report Configuration)
/// </summary>
public class ReportDefinition
{
    public uint ReportId { get; }
    /// <summary>レポートに含まれる変数ID (SVID, DVID, ECID)</summary>
    public List<uint> VariableIds { get; }

    public ReportDefinition(uint reportId, IEnumerable<uint> variableIds)
    {
        ReportId = reportId;
        VariableIds = new List<uint>(variableIds);
    }
}
