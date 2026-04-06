using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Secs4Net;
using SampleEqSim.Core.Gem;
using Xunit;

namespace SampleEqSim.Tests;

/// <summary>
/// GEM 状態機械の単体テスト
/// </summary>
public class GemStateTests
{
    private readonly Mock<ISecsGem> _secsGemMock;
    private readonly GemEquipmentModel _model;

    public GemStateTests()
    {
        _secsGemMock = new Mock<ISecsGem>();
        // ConnectionChanged と PrimaryMessageReceived のイベント登録を設定
        _secsGemMock.SetupAdd(m => m.ConnectionChanged += It.IsAny<EventHandler<ConnectionState>>());
        _secsGemMock.SetupAdd(m => m.PrimaryMessageReceived += It.IsAny<EventHandler<PrimaryMessageWrapper>>());

        _model = new GemEquipmentModel(
            _secsGemMock.Object,
            NullLogger<GemEquipmentModel>.Instance);
    }

    [Fact]
    public void InitialState_ShouldBeDisabledAndOffline()
    {
        Assert.Equal(CommunicationState.Disabled, _model.CommunicationState);
        Assert.Equal(ControlState.EquipmentOffline, _model.ControlState);
        Assert.Equal(ProcessingState.Init, _model.ProcessingState);
    }

    [Fact]
    public void ProcessingStateTransition_IdleToExecuting_ShouldWork()
    {
        _model.SetProcessingState(ProcessingState.Idle);
        Assert.Equal(ProcessingState.Idle, _model.ProcessingState);

        _model.SetProcessingState(ProcessingState.Executing);
        Assert.Equal(ProcessingState.Executing, _model.ProcessingState);
    }

    [Fact]
    public void ProcessingStateChanged_EventShouldFire()
    {
        ProcessingState? capturedState = null;
        _model.ProcessingStateChanged += (_, s) => capturedState = s;

        _model.SetProcessingState(ProcessingState.Ready);

        Assert.Equal(ProcessingState.Ready, capturedState);
    }

    [Fact]
    public void AlarmDefinitions_ShouldBeInitialized()
    {
        Assert.NotEmpty(_model.Alarms);
        Assert.True(_model.Alarms.ContainsKey(1));
        Assert.True(_model.Alarms.ContainsKey(2));
    }

    [Fact]
    public void StatusVariables_ShouldContainStandardSVIDs()
    {
        Assert.True(_model.StatusVariables.ContainsKey(1));   // ClockTime
        Assert.True(_model.StatusVariables.ContainsKey(2));   // ControlState
        Assert.True(_model.StatusVariables.ContainsKey(3));   // ProcessingState
        Assert.True(_model.StatusVariables.ContainsKey(4));   // CommunicationState
    }

    [Fact]
    public void EquipmentConstants_ShouldContainT7()
    {
        Assert.True(_model.EquipmentConstants.ContainsKey(1)); // EstablishCommunicationsTimeout
        var ec = _model.EquipmentConstants[1];
        Assert.Equal("EstablishCommunicationsTimeout", ec.ConstantName);
    }

    [Fact]
    public void CollectionEvents_ShouldBeInitialized()
    {
        Assert.True(_model.CollectionEvents.ContainsKey(1));   // EquipmentOffline
        Assert.True(_model.CollectionEvents.ContainsKey(101)); // ProcessStarted
        Assert.True(_model.CollectionEvents.ContainsKey(102)); // ProcessCompleted
    }

    [Fact]
    public void StatusVariable_ClockTime_ShouldReturnCurrentTime()
    {
        var sv = _model.StatusVariables[1];
        var value = sv.GetValue()?.ToString();
        Assert.NotNull(value);
        Assert.Equal(14, value.Length); // yyyyMMddHHmmss
    }

    [Fact]
    public void SetTemperature_ShouldUpdateStatusVariable()
    {
        _model.SetTemperature(150.0f);
        var sv = _model.StatusVariables[101];
        var value = sv.GetValue();
        Assert.Equal(150.0f, (float)value);
    }

    [Fact]
    public void DefineReport_ShouldStoreReportDefinition()
    {
        var rpt = new ReportDefinition(1, new[] { 101u, 102u, 103u });
        _model.Reports[1] = rpt;

        Assert.True(_model.Reports.ContainsKey(1));
        Assert.Equal(3, _model.Reports[1].VariableIds.Count);
    }

    [Fact]
    public void EventReportLink_ShouldBeConfigurable()
    {
        _model.EventReportLinks[101] = new List<uint> { 1u };

        Assert.True(_model.EventReportLinks.ContainsKey(101));
        Assert.Contains(1u, _model.EventReportLinks[101]);
    }

    [Fact]
    public void EnabledEvents_ShouldAllBeEnabledByDefault()
    {
        foreach (var ceid in _model.CollectionEvents.Keys)
        {
            Assert.True(_model.EnabledEvents.TryGetValue(ceid, out var enabled));
            Assert.True(enabled);
        }
    }

    [Fact]
    public async Task SetAlarm_WhenNotCommunicating_ShouldNotThrow()
    {
        // 通信していない場合はS5F1を送信しないことを確認
        var ex = await Record.ExceptionAsync(() => _model.SetAlarmAsync(1, true));
        Assert.Null(ex);
        Assert.True(_model.Alarms[1].IsSet);
    }
}
