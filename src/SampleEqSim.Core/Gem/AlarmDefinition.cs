namespace SampleEqSim.Core.Gem;

/// <summary>アラームカテゴリ</summary>
public enum AlarmCategory : byte
{
    /// <summary>個人の安全に関わる</summary>
    Personal = 1,
    /// <summary>装置安全に関わる</summary>
    Equipment = 2,
    /// <summary>測定/制御の警告</summary>
    Warning = 4,
    /// <summary>重大な障害</summary>
    Fault = 8,
    /// <summary>情報</summary>
    Information = 16,
}

/// <summary>
/// GEM アラーム定義 (SEMI E30 §10.6)
/// </summary>
public class AlarmDefinition
{
    public uint AlarmId { get; }
    public string AlarmCode { get; }
    public string AlarmText { get; }
    public AlarmCategory Category { get; }
    public bool IsEnabled { get; set; } = true;
    public bool IsSet { get; set; } = false;

    public AlarmDefinition(uint alarmId, string alarmCode, string alarmText, AlarmCategory category)
    {
        AlarmId = alarmId;
        AlarmCode = alarmCode;
        AlarmText = alarmText;
        Category = category;
    }
}
