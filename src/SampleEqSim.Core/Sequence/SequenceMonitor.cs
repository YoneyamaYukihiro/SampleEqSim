using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SampleEqSim.Core.Sequence;

/// <summary>シーケンス1ステップの状態。</summary>
public enum StepStatus
{
    /// <summary>未到達</summary>
    Pending,
    /// <summary>次に期待</summary>
    Active,
    /// <summary>受信/送信済</summary>
    Done,
    /// <summary>スキップ (期待されたのに来ないまま先のメッセージが来た)</summary>
    Skipped,
}

/// <summary>想定シーケンスの1ステップ (実行時に状態を持つ)。</summary>
public sealed class MonitorStep
{
    public string Sf { get; }
    public string Desc { get; }
    public StepStatus Status { get; internal set; } = StepStatus.Pending;
    public string? TimeText { get; internal set; }

    public MonitorStep(string sf, string desc)
    {
        Sf = sf;
        Desc = desc;
    }
}

/// <summary>
/// 想定シーケンス (S/F の順序) に対して、実際に送受信されたメッセージを時系列で照合し、
/// スキップ・順序逸脱・想定外メッセージを検出する監視器。
/// </summary>
public sealed class SequenceMonitor
{
    private readonly Func<DateTime> _now;
    private int _pointer; // 次に照合を始めるインデックス

    public string Name { get; }
    public IReadOnlyList<MonitorStep> Steps { get; }
    public int UnexpectedCount { get; private set; }
    public string? LastUnexpected { get; private set; }

    public SequenceMonitor(SequenceDefinition definition, Func<DateTime>? now = null)
    {
        _now = now ?? (() => DateTime.Now);
        Name = definition.Name;
        Steps = definition.Steps.Select(s => new MonitorStep(s.Sf, s.Desc)).ToList();
        Reset();
    }

    /// <summary>スキップまたは想定外メッセージが発生したか。</summary>
    public bool HasDeviation =>
        UnexpectedCount > 0 || Steps.Any(s => s.Status == StepStatus.Skipped);

    /// <summary>全ステップが Done/Skipped で確定したか。</summary>
    public bool IsComplete =>
        Steps.Count > 0 && Steps.All(s => s.Status is StepStatus.Done or StepStatus.Skipped);

    /// <summary>次に期待しているステップ (無ければ null)。</summary>
    public MonitorStep? NextExpected =>
        Steps.FirstOrDefault(s => s.Status == StepStatus.Active);

    /// <summary>ヘッダー表示用の状態サマリ。</summary>
    public string Summary
    {
        get
        {
            if (HasDeviation)
            {
                var detail = UnexpectedCount > 0 ? $"想定外 {LastUnexpected} を{UnexpectedCount}件受信" : "スキップあり";
                return $"⚠ 逸脱検出 ({detail})";
            }
            if (IsComplete) return "✅ シーケンス完了";
            return NextExpected is { } n ? $"監視中… 次に期待: {n.Sf}" : "監視中…";
        }
    }

    /// <summary>逸脱の有無に応じた LED 色キー。</summary>
    public string SummaryBrush =>
        HasDeviation ? "LedRed" : IsComplete ? "LedGreen" : "LedYellow";

    /// <summary>送受信されたメッセージを時系列順に1件投入して照合する。</summary>
    public void Observe(byte s, byte f)
    {
        var label = $"S{s}F{f}";

        // _pointer 以降で、まだ確定していない一致ステップを探す
        int match = -1;
        for (int i = _pointer; i < Steps.Count; i++)
        {
            if (Steps[i].Status is StepStatus.Pending or StepStatus.Active &&
                string.Equals(Steps[i].Sf, label, StringComparison.OrdinalIgnoreCase))
            {
                match = i;
                break;
            }
        }

        if (match < 0)
        {
            // 想定シーケンスに無い (または既に通過済みの) メッセージ
            UnexpectedCount++;
            LastUnexpected = label;
            return;
        }

        // 一致より手前の未確定ステップはスキップ扱い
        for (int k = _pointer; k < match; k++)
            if (Steps[k].Status is StepStatus.Pending or StepStatus.Active)
                Steps[k].Status = StepStatus.Skipped;

        Steps[match].Status = StepStatus.Done;
        Steps[match].TimeText = _now().ToString("HH:mm:ss.fff");
        _pointer = match + 1;
        SetNextActive();
    }

    /// <summary>監視状態を初期化する。</summary>
    public void Reset()
    {
        _pointer = 0;
        UnexpectedCount = 0;
        LastUnexpected = null;
        foreach (var step in Steps)
        {
            step.Status = StepStatus.Pending;
            step.TimeText = null;
        }
        SetNextActive();
    }

    private void SetNextActive()
    {
        for (int i = _pointer; i < Steps.Count; i++)
        {
            if (Steps[i].Status == StepStatus.Pending)
            {
                Steps[i].Status = StepStatus.Active;
                return;
            }
        }
    }
}

/// <summary>想定シーケンス定義 (JSON から読み込み)。</summary>
public sealed record SequenceDefinition(string Name, IReadOnlyList<SequenceStepDef> Steps);

/// <summary>想定シーケンスの1ステップ定義。</summary>
public sealed record SequenceStepDef(string Sf, string Desc);

/// <summary>
/// 想定シーケンス定義 (sequence.json) のロード。ファイルが無ければ既定 (ONLINE 確立) を返し、
/// テンプレートとして書き出す。
/// </summary>
public static class SequenceLoader
{
    public static string DefaultPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "sequence.json");

    /// <summary>既定の想定シーケンス: GEM ONLINE 確立。</summary>
    public static SequenceDefinition BuiltInDefault { get; } = new(
        "ONLINE確立シーケンス",
        new[]
        {
            new SequenceStepDef("S1F13", "通信確立要求 (Host→Eq)"),
            new SequenceStepDef("S1F14", "通信確立応答 (Eq→Host)"),
            new SequenceStepDef("S1F17", "オンライン要求 (Host→Eq)"),
            new SequenceStepDef("S1F18", "オンライン応答 (Eq→Host)"),
        });

    /// <summary>
    /// 指定パス (既定は <see cref="DefaultPath"/>) から読み込む。
    /// ファイルが無ければ既定を書き出して返す。読み込み失敗時も既定を返す。
    /// </summary>
    public static SequenceDefinition Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path))
            {
                TryWrite(path, BuiltInDefault);
                return BuiltInDefault;
            }

            var dto = JsonSerializer.Deserialize<SequenceDto>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dto?.Steps is not { Count: > 0 })
                return BuiltInDefault;

            return new SequenceDefinition(
                string.IsNullOrWhiteSpace(dto.Name) ? "(無題)" : dto.Name,
                dto.Steps.Select(s => new SequenceStepDef(s.Sf ?? "", s.Desc ?? "")).ToList());
        }
        catch
        {
            return BuiltInDefault;
        }
    }

    private static void TryWrite(string path, SequenceDefinition def)
    {
        try
        {
            var dto = new SequenceDto
            {
                Name = def.Name,
                Steps = def.Steps.Select(s => new StepDto { Sf = s.Sf, Desc = s.Desc }).ToList(),
            };
            File.WriteAllText(path,
                JsonSerializer.Serialize(dto, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // 日本語 desc をエスケープせずそのまま出力し、手編集しやすくする
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                }));
        }
        catch { /* テンプレート書き出し失敗は無視 */ }
    }

    private sealed class SequenceDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("steps")] public List<StepDto>? Steps { get; set; }
    }

    private sealed class StepDto
    {
        [JsonPropertyName("sf")] public string? Sf { get; set; }
        [JsonPropertyName("desc")] public string? Desc { get; set; }
    }
}
