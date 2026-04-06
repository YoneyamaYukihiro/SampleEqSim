using System.Collections.Generic;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Secs4Net;
using SampleEqSim.Core.Gem;
using Xunit;
using static Secs4Net.Item;

namespace SampleEqSim.Tests;

/// <summary>
/// GEM 繝｡繝・そ繝ｼ繧ｸ繝上Φ繝峨Λ繝ｼ縺ｮ蜊倅ｽ薙ユ繧ｹ繝・
/// secs4net縺ｮPrimaryMessageReceived繧帝壹§縺ｦ繝｡繝・そ繝ｼ繧ｸ繧呈ｳｨ蜈･縺励・
/// 霑皮ｭ斐・蜀・ｮｹ繧呈､懆ｨｼ縺吶ｋ
/// </summary>
public class GemMessageHandlerTests
{
    private readonly GemEquipmentModel _model;
    
    public GemMessageHandlerTests()
    {
        var secsGemMock = new Mock<ISecsGem>();
        // SendAsync縺ｯ謌仙粥繧定ｿ斐☆
        secsGemMock.Setup(m => m.SendAsync(It.IsAny<SecsMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SecsMessage(1, 2));

        _model = new GemEquipmentModel(
            secsGemMock.Object,
            NullLogger<GemEquipmentModel>.Instance);
    }

    [Fact]
    public void GemModel_ShouldHaveStandardAlarms()
    {
        Assert.True(_model.Alarms.ContainsKey(1));
        Assert.Equal("LOW_AIR", _model.Alarms[1].AlarmCode);
        Assert.True(_model.Alarms[1].IsEnabled);
        Assert.False(_model.Alarms[1].IsSet);
    }

    [Fact]
    public void GemModel_ShouldHaveDataVariables()
    {
        Assert.True(_model.DataVariables.ContainsKey(1001)); // ProcessTemp
        Assert.True(_model.DataVariables.ContainsKey(1002)); // ProcessPressure
    }

    [Fact]
    public void ReportDefinition_Creation_ShouldWork()
    {
        var rpt = new ReportDefinition(99, new[] { 101u, 102u });
        Assert.Equal(99u, rpt.ReportId);
        Assert.Equal(2, rpt.VariableIds.Count);
    }

    [Fact]
    public void TraceDefinition_Creation_ShouldWork()
    {
        var trace = new TraceDefinition(1, 5, 100, new[] { 1001u, 1002u });
        Assert.Equal(1u, trace.TraceRequestId);
        Assert.Equal(5u, trace.SamplingPeriod);
        Assert.Equal(100u, trace.TotalSamples);
        Assert.Equal(2, trace.VariableIds.Count);
    }

    [Fact]
    public void AlarmDefinition_Category_ShouldBeSet()
    {
        var alarm = new AlarmDefinition(1, "TEST", "Test alarm", AlarmCategory.Warning);
        Assert.Equal(AlarmCategory.Warning, alarm.Category);
    }

    [Fact]
    public void EquipmentConstant_CurrentValue_ShouldBeSettable()
    {
        var ec = new EquipmentConstant(1, "TestEC", "U2", (ushort)10, (ushort)0, (ushort)100);
        Assert.Equal((ushort)10, ec.CurrentValue);

        ec.CurrentValue = (ushort)50;
        Assert.Equal((ushort)50, ec.CurrentValue);
    }

    [Fact]
    public void StatusVariable_GetValue_ShouldUseLambda()
    {
        int counter = 0;
        var sv = new StatusVariable(999, "Counter", "U4", () => (uint)(++counter));
        Assert.Equal(1u, (uint)sv.GetValue());
        Assert.Equal(2u, (uint)sv.GetValue());
    }

    [Fact]
    public void CollectionEventDefinition_ShouldHaveIdAndName()
    {
        var ce = new CollectionEventDefinition(100, "TestEvent");
        Assert.Equal(100u, ce.CollectionEventId);
        Assert.Equal("TestEvent", ce.EventName);
    }

    [Fact]
    public void LimitDefinition_ShouldBeCreatable()
    {
        var vla = new VariableLimitAttribute(101);
        vla.Limits.Add(new LimitPair
        {
            LimitId = 1,
            UpperCollectionEventId = 200,
            LowerCollectionEventId = 201
        });
        Assert.Single(vla.Limits);
        Assert.Equal(101u, vla.VariableId);
    }
}


