using SampleEqSim.Core.Sequence;
using Xunit;

namespace SampleEqSim.Tests;

/// <summary>
/// シーケンス監視 (想定順序との照合・スキップ/逸脱検出) のテスト。
/// </summary>
public class SequenceMonitorTests
{
    private static SequenceMonitor NewOnlineMonitor()
        => new(new SequenceDefinition("ONLINE", new[]
        {
            new SequenceStepDef("S1F13", ""),
            new SequenceStepDef("S1F14", ""),
            new SequenceStepDef("S1F17", ""),
            new SequenceStepDef("S1F18", ""),
        }));

    [Fact]
    public void Initial_FirstStepActive_RestPending()
    {
        var m = NewOnlineMonitor();

        Assert.Equal(StepStatus.Active, m.Steps[0].Status);
        Assert.Equal(StepStatus.Pending, m.Steps[1].Status);
        Assert.False(m.HasDeviation);
        Assert.False(m.IsComplete);
        Assert.Equal("S1F13", m.NextExpected!.Sf);
    }

    [Fact]
    public void InOrder_AllDone_Complete_NoDeviation()
    {
        var m = NewOnlineMonitor();

        m.Observe(1, 13);
        m.Observe(1, 14);
        m.Observe(1, 17);
        m.Observe(1, 18);

        Assert.All(m.Steps, s => Assert.Equal(StepStatus.Done, s.Status));
        Assert.True(m.IsComplete);
        Assert.False(m.HasDeviation);
        Assert.All(m.Steps, s => Assert.NotNull(s.TimeText));
    }

    [Fact]
    public void SkippedStep_MarkedSkipped_AndDeviation()
    {
        var m = NewOnlineMonitor();

        m.Observe(1, 13);          // S1F13 done
        m.Observe(1, 17);          // S1F14 をスキップして S1F17 が来た

        Assert.Equal(StepStatus.Done,    m.Steps[0].Status); // S1F13
        Assert.Equal(StepStatus.Skipped, m.Steps[1].Status); // S1F14 (スキップ)
        Assert.Equal(StepStatus.Done,    m.Steps[2].Status); // S1F17
        Assert.Equal(StepStatus.Active,  m.Steps[3].Status); // S1F18 (次に期待)
        Assert.True(m.HasDeviation);
    }

    [Fact]
    public void UnexpectedMessage_Counted_AsDeviation()
    {
        var m = NewOnlineMonitor();

        m.Observe(1, 13);
        m.Observe(6, 11);          // 想定外 (S6F11 はシーケンスに無い)

        Assert.Equal(1, m.UnexpectedCount);
        Assert.Equal("S6F11", m.LastUnexpected);
        Assert.True(m.HasDeviation);
        // 想定外メッセージは進行ポインタを動かさない
        Assert.Equal(StepStatus.Active, m.Steps[1].Status); // S1F14 は依然 次に期待
    }

    [Fact]
    public void Reset_RestoresInitialState()
    {
        var m = NewOnlineMonitor();
        m.Observe(1, 13);
        m.Observe(6, 11);

        m.Reset();

        Assert.Equal(StepStatus.Active, m.Steps[0].Status);
        Assert.Equal(0, m.UnexpectedCount);
        Assert.False(m.HasDeviation);
        Assert.All(m.Steps.Skip(1), s => Assert.Equal(StepStatus.Pending, s.Status));
    }

    [Fact]
    public void Loader_MissingFile_ReturnsBuiltInDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"seq-missing-{Guid.NewGuid():N}.json");
        try
        {
            var def = SequenceLoader.Load(path);
            Assert.Equal(BuiltInName(), def.Name);
            Assert.Equal(4, def.Steps.Count);
            Assert.Equal("S1F13", def.Steps[0].Sf);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static string BuiltInName() => SequenceLoader.BuiltInDefault.Name;
}
