using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secs4Net;
using static Secs4Net.Item;

namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM Full Compliant 装置モデル (SEMI E30)
/// IHostedService として起動し、GetPrimaryMessageAsync でメッセージを処理する
/// </summary>
public class GemEquipmentModel : IHostedService
{
    private readonly ISecsGem _secsGem;
    private readonly ILogger<GemEquipmentModel> _logger;

    // ─── State Machines ──────────────────────────────────────────
    private CommunicationState _communicationState = CommunicationState.Disabled;
    private ControlState _controlState = ControlState.EquipmentOffline;
    private ProcessingState _processingState = ProcessingState.Init;

    // ─── GEM Data Model ──────────────────────────────────────────
    public Dictionary<uint, AlarmDefinition> Alarms { get; } = new();
    public Dictionary<uint, CollectionEventDefinition> CollectionEvents { get; } = new();
    public Dictionary<uint, StatusVariable> StatusVariables { get; } = new();
    public Dictionary<uint, DataVariable> DataVariables { get; } = new();
    public Dictionary<uint, EquipmentConstant> EquipmentConstants { get; } = new();
    public Dictionary<uint, ReportDefinition> Reports { get; } = new();
    /// <summary>イベント → レポートID のリンクテーブル</summary>
    public Dictionary<uint, List<uint>> EventReportLinks { get; } = new();
    /// <summary>イベントの有効/無効</summary>
    public Dictionary<uint, bool> EnabledEvents { get; } = new();
    public Dictionary<uint, TraceDefinition> TraceRequests { get; } = new();
    public Dictionary<uint, VariableLimitAttribute> VariableLimits { get; } = new();

    // ─── Equipment Info ──────────────────────────────────────────
    public string ModelName { get; set; } = "SampleEquipment";
    public string SoftRev { get; set; } = "1.0.0";

    // ─── Events ──────────────────────────────────────────────────
    public event EventHandler<CommunicationState>? CommunicationStateChanged;
    public event EventHandler<ControlState>? ControlStateChanged;
    public event EventHandler<ProcessingState>? ProcessingStateChanged;
    public event EventHandler<string>? MessageLogged;
    public event EventHandler<(uint AlarmId, bool IsSet)>? AlarmStateChanged;

    // ─── Properties ──────────────────────────────────────────────
    public CommunicationState CommunicationState => _communicationState;
    public ControlState ControlState => _controlState;
    public ProcessingState ProcessingState => _processingState;

    // T7 タイマー
    private System.Timers.Timer? _t7Timer;

    public GemEquipmentModel(ISecsGem secsGem, ILogger<GemEquipmentModel> logger)
    {
        _secsGem = secsGem;
        _logger = logger;

        // ISecsConnection にキャストして接続状態変化を購読
        if (secsGem is ISecsConnection connection)
            connection.ConnectionChanged += OnConnectionChanged;

        InitializeGemData();
    }

    // ─────────────────────────────────────────────────────────────
    // IHostedService
    // ─────────────────────────────────────────────────────────────
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var e in _secsGem.GetPrimaryMessageAsync(cancellationToken))
            {
                await HandlePrimaryMessageAsync(e);
            }
        }
        catch (OperationCanceledException)
        {
            // 正常終了
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "メッセージループエラー");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_secsGem is ISecsConnection connection)
            connection.ConnectionChanged -= OnConnectionChanged;
        StopT7Timer();
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────
    // 初期化
    // ─────────────────────────────────────────────────────────────
    private void InitializeGemData()
    {
        // ── Status Variables (SVID) ──
        StatusVariables[1] = new StatusVariable(1, "ClockTime", "A", () =>
            DateTime.Now.ToString("yyyyMMddHHmmss"));
        StatusVariables[2] = new StatusVariable(2, "ControlState", "U1", () =>
            (byte)_controlState);
        StatusVariables[3] = new StatusVariable(3, "ProcessingState", "U1", () =>
            (byte)_processingState);
        StatusVariables[4] = new StatusVariable(4, "CommunicationState", "U1", () =>
            (byte)_communicationState);
        StatusVariables[5] = new StatusVariable(5, "EventsEnabled", "BOOLEAN", () =>
            EnabledEvents.Values.Any(v => v));
        StatusVariables[6] = new StatusVariable(6, "AlarmsEnabled", "BOOLEAN", () =>
            Alarms.Values.Any(a => a.IsEnabled));
        StatusVariables[7] = new StatusVariable(7, "AlarmsSet", "BOOLEAN", () =>
            Alarms.Values.Any(a => a.IsSet));
        StatusVariables[101] = new StatusVariable(101, "Temperature", "F4", () =>
            _temperature, "degC");
        StatusVariables[102] = new StatusVariable(102, "Pressure", "F4", () =>
            _pressure, "kPa");
        StatusVariables[103] = new StatusVariable(103, "LotId", "A", () =>
            _lotId);
        StatusVariables[104] = new StatusVariable(104, "RecipeId", "A", () =>
            _recipeId);

        // ── Data Variables (DVID) ──
        DataVariables[1001] = new DataVariable(1001, "ProcessTemp", "F4", () =>
            _temperature, "degC");
        DataVariables[1002] = new DataVariable(1002, "ProcessPressure", "F4", () =>
            _pressure, "kPa");
        DataVariables[1003] = new DataVariable(1003, "ProcessTime", "U4", () =>
            (uint)_processElapsedSeconds, "sec");

        // ── Equipment Constants (ECID) ──
        EquipmentConstants[1] = new EquipmentConstant(1, "EstablishCommunicationsTimeout",
            "U2", (ushort)10, (ushort)0, (ushort)240, "sec");
        EquipmentConstants[2] = new EquipmentConstant(2, "TimeFormat",
            "U1", (byte)1, (byte)0, (byte)2);
        EquipmentConstants[101] = new EquipmentConstant(101, "MaxTemperature",
            "F4", 200.0f, 0.0f, 500.0f, "degC");
        EquipmentConstants[102] = new EquipmentConstant(102, "MaxPressure",
            "F4", 100.0f, 0.0f, 200.0f, "kPa");
        EquipmentConstants[103] = new EquipmentConstant(103, "ProcessTimeout",
            "U4", (uint)3600, (uint)0, (uint)86400, "sec");

        // ── Collection Events (CEID) ──
        CollectionEvents[1] = new CollectionEventDefinition(1, "EquipmentOffline");
        CollectionEvents[2] = new CollectionEventDefinition(2, "ControlStateLocal");
        CollectionEvents[3] = new CollectionEventDefinition(3, "ControlStateRemote");
        CollectionEvents[4] = new CollectionEventDefinition(4, "ProcessingStateChange");
        CollectionEvents[5] = new CollectionEventDefinition(5, "AlarmOccurred");
        CollectionEvents[6] = new CollectionEventDefinition(6, "AlarmCleared");
        CollectionEvents[7] = new CollectionEventDefinition(7, "CommandInitiated");
        CollectionEvents[8] = new CollectionEventDefinition(8, "CommandCompleted");
        CollectionEvents[101] = new CollectionEventDefinition(101, "ProcessStarted");
        CollectionEvents[102] = new CollectionEventDefinition(102, "ProcessCompleted");
        CollectionEvents[103] = new CollectionEventDefinition(103, "LotStarted");
        CollectionEvents[104] = new CollectionEventDefinition(104, "LotCompleted");

        foreach (var ceid in CollectionEvents.Keys)
            EnabledEvents[ceid] = true;

        // ── Alarms (ALID) ──
        Alarms[1] = new AlarmDefinition(1, "LOW_AIR", "低圧縮空気検出", AlarmCategory.Fault);
        Alarms[2] = new AlarmDefinition(2, "HIGH_TEMP", "高温検出", AlarmCategory.Warning);
        Alarms[3] = new AlarmDefinition(3, "DOOR_OPEN", "処理中のドア開", AlarmCategory.Fault);
        Alarms[4] = new AlarmDefinition(4, "POWER_FAIL", "電源異常", AlarmCategory.Equipment);
        Alarms[5] = new AlarmDefinition(5, "COMM_ERROR", "通信エラー", AlarmCategory.Warning);
    }

    // ─────────────────────────────────────────────────────────────
    // シミュレーション用データ
    // ─────────────────────────────────────────────────────────────
    private float _temperature = 25.0f;
    private float _pressure = 101.3f;
    private string _lotId = "";
    private string _recipeId = "";
    private uint _processElapsedSeconds = 0;

    public void SetTemperature(float value) => _temperature = value;
    public void SetPressure(float value) => _pressure = value;
    public void SetLotId(string value) => _lotId = value;
    public void SetRecipeId(string value) => _recipeId = value;

    // ─────────────────────────────────────────────────────────────
    // 接続状態変化 (ISecsConnection.ConnectionChanged)
    // ─────────────────────────────────────────────────────────────
    private void OnConnectionChanged(object? sender, ConnectionState state)
    {
        Log($"[HSMS] 接続状態変化: {state}");
        switch (state)
        {
            case ConnectionState.Connected:
            case ConnectionState.Selected:
                SetCommunicationState(CommunicationState.WaitCrFromHost);
                StartT7Timer();
                break;
            case ConnectionState.Retry:
            case ConnectionState.Connecting:
                StopT7Timer();
                SetCommunicationState(CommunicationState.Disabled);
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // T7 タイマー
    // ─────────────────────────────────────────────────────────────
    private void StartT7Timer()
    {
        var timeoutSec = Convert.ToDouble(EquipmentConstants[1].CurrentValue);
        _t7Timer?.Dispose();
        _t7Timer = new System.Timers.Timer(timeoutSec * 1000);
        _t7Timer.Elapsed += (_, _) =>
        {
            _t7Timer?.Dispose();
            _t7Timer = null;
            Log("[T7] タイムアウト: 通信確立失敗");
            SetCommunicationState(CommunicationState.WaitDelay);
        };
        _t7Timer.AutoReset = false;
        _t7Timer.Start();
    }

    private void StopT7Timer()
    {
        _t7Timer?.Stop();
        _t7Timer?.Dispose();
        _t7Timer = null;
    }

    // ─────────────────────────────────────────────────────────────
    // 状態遷移
    // ─────────────────────────────────────────────────────────────
    private void SetCommunicationState(CommunicationState newState)
    {
        if (_communicationState == newState) return;
        Log($"[ComState] {_communicationState} → {newState}");
        _communicationState = newState;
        CommunicationStateChanged?.Invoke(this, newState);
    }

    private void SetControlState(ControlState newState)
    {
        if (_controlState == newState) return;
        Log($"[CtrlState] {_controlState} → {newState}");
        _controlState = newState;
        ControlStateChanged?.Invoke(this, newState);

        var ceid = newState switch
        {
            ControlState.EquipmentOffline => 1u,
            ControlState.OnlineLocal      => 2u,
            ControlState.OnlineRemote     => 3u,
            _                             => 0u,
        };
        _ = SendCollectionEventAsync(ceid);
    }

    public void SetProcessingState(ProcessingState newState)
    {
        if (_processingState == newState) return;
        Log($"[ProcState] {_processingState} → {newState}");
        _processingState = newState;
        ProcessingStateChanged?.Invoke(this, newState);
        _ = SendCollectionEventAsync(4u); // ProcessingStateChange
    }

    // ─────────────────────────────────────────────────────────────
    // アラーム操作
    // ─────────────────────────────────────────────────────────────
    public async Task SetAlarmAsync(uint alarmId, bool set)
    {
        if (!Alarms.TryGetValue(alarmId, out var alarm)) return;
        if (alarm.IsSet == set) return;

        alarm.IsSet = set;
        AlarmStateChanged?.Invoke(this, (alarmId, set));

        if (alarm.IsEnabled && _communicationState == CommunicationState.Communicating)
        {
            var msg = new SecsMessage(5, 1, "S5F1")
            {
                SecsItem = L(
                    B((byte)(set ? 0x81 : 0x01)),
                    U4(alarm.AlarmId),
                    A(alarm.AlarmText))
            };
            try
            {
                Log($"SND >> S5F1 ALARM {(set ? "SET" : "CLR")} [{alarm.AlarmCode}]");
                await _secsGem.SendAsync(msg);
            }
            catch (Exception ex)
            {
                Log($"[ERR] S5F1 送信失敗: {ex.Message}");
            }
        }

        await SendCollectionEventAsync(set ? 5u : 6u);
    }

    // ─────────────────────────────────────────────────────────────
    // イベントレポート送信
    // ─────────────────────────────────────────────────────────────
    public async Task SendCollectionEventAsync(uint ceid)
    {
        if (ceid == 0) return;
        if (_communicationState != CommunicationState.Communicating) return;
        if (!EnabledEvents.TryGetValue(ceid, out var enabled) || !enabled) return;
        if (!CollectionEvents.ContainsKey(ceid)) return;

        var reportItems = new List<Item>();
        if (EventReportLinks.TryGetValue(ceid, out var rptIds))
        {
            foreach (var rptId in rptIds)
            {
                if (!Reports.TryGetValue(rptId, out var rpt)) continue;
                var varItems = rpt.VariableIds
                    .Select(vid => BuildVariableValueItem(vid))
                    .ToList();
                reportItems.Add(L(U4(rptId), L(varItems)));
            }
        }

        var s6f11 = new SecsMessage(6, 11, "S6F11")
        {
            SecsItem = L(
                U4(0),
                U4(ceid),
                L(reportItems))
        };

        try
        {
            Log($"SND >> S6F11 CEID={ceid} [{CollectionEvents[ceid].EventName}]");
            await _secsGem.SendAsync(s6f11);
        }
        catch (Exception ex)
        {
            Log($"[ERR] S6F11 送信失敗: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 変数値ビルド
    // ─────────────────────────────────────────────────────────────
    private Item BuildVariableValueItem(uint vid)
    {
        if (StatusVariables.TryGetValue(vid, out var sv))
            return BuildTypedItem(sv.Format, sv.GetValue());
        if (DataVariables.TryGetValue(vid, out var dv))
            return BuildTypedItem(dv.Format, dv.GetValue());
        if (EquipmentConstants.TryGetValue(vid, out var ec))
            return BuildTypedItem(ec.Format, ec.CurrentValue);
        return A("");
    }

    private static Item BuildTypedItem(string format, object value)
    {
        return format.ToUpperInvariant() switch
        {
            "A"              => A(value?.ToString() ?? ""),
            "U1"             => U1(Convert.ToByte(value)),
            "U2"             => U2(Convert.ToUInt16(value)),
            "U4"             => U4(Convert.ToUInt32(value)),
            "I1"             => I1(Convert.ToSByte(value)),
            "I2"             => I2(Convert.ToInt16(value)),
            "I4"             => I4(Convert.ToInt32(value)),
            "F4"             => F4(Convert.ToSingle(value)),
            "F8"             => F8(Convert.ToDouble(value)),
            "BOOLEAN"
            or "BOOL"        => Boolean(Convert.ToBoolean(value)),
            _                => A(value?.ToString() ?? ""),
        };
    }

    // ─────────────────────────────────────────────────────────────
    // メッセージ処理
    // ─────────────────────────────────────────────────────────────
    private async Task HandlePrimaryMessageAsync(PrimaryMessageWrapper e)
    {
        var msg = e.PrimaryMessage;
        Log($"RCV << S{msg.S}F{msg.F} {msg.Name}");

        try
        {
            SecsMessage? reply = (msg.S, msg.F) switch
            {
                (1, 1)   => HandleS1F1(msg),
                (1, 3)   => HandleS1F3(msg),
                (1, 11)  => HandleS1F11(msg),
                (1, 13)  => HandleS1F13(msg),
                (1, 15)  => HandleS1F15(msg),
                (1, 17)  => HandleS1F17(msg),
                (2, 13)  => HandleS2F13(msg),
                (2, 15)  => HandleS2F15(msg),
                (2, 17)  => HandleS2F17(msg),
                (2, 23)  => HandleS2F23(msg),
                (2, 29)  => HandleS2F29(msg),
                (2, 31)  => HandleS2F31(msg),
                (2, 33)  => HandleS2F33(msg),
                (2, 35)  => HandleS2F35(msg),
                (2, 37)  => HandleS2F37(msg),
                (2, 41)  => HandleS2F41(msg),
                (2, 45)  => HandleS2F45(msg),
                (2, 47)  => HandleS2F47(msg),
                (5, 3)   => HandleS5F3(msg),
                (5, 5)   => HandleS5F5(msg),
                (5, 7)   => HandleS5F7(msg),
                (6, 15)  => HandleS6F15(msg),
                (6, 17)  => HandleS6F17(msg),
                (6, 19)  => HandleS6F19(msg),
                (10, 3)  => HandleS10F3(msg),
                (10, 5)  => HandleS10F5(msg),
                _        => HandleUnknown(msg),
            };

            if (msg.ReplyExpected && reply != null)
            {
                Log($"SND >> S{reply.S}F{reply.F} {reply.Name}");
                await e.ReplyAsync(reply);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "メッセージ処理エラー S{S}F{F}", msg.S, msg.F);
        }
    }

    // ═════════════════════════════════════════════════════════════
    // Stream 1: Equipment Status
    // ═════════════════════════════════════════════════════════════

    private SecsMessage HandleS1F1(SecsMessage msg) =>
        new SecsMessage(1, 2, "S1F2")
        {
            SecsItem = L(A(ModelName), A(SoftRev))
        };

    private SecsMessage HandleS1F3(SecsMessage msg)
    {
        var svIds = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : StatusVariables.Keys.ToList();

        return new SecsMessage(1, 4, "S1F4")
        {
            SecsItem = L(svIds.Select(id =>
                StatusVariables.TryGetValue(id, out var sv)
                    ? BuildTypedItem(sv.Format, sv.GetValue())
                    : L()))
        };
    }

    private SecsMessage HandleS1F11(SecsMessage msg)
    {
        var svIds = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : StatusVariables.Keys.ToList();

        return new SecsMessage(1, 12, "S1F12")
        {
            SecsItem = L(svIds.Select(id =>
                StatusVariables.TryGetValue(id, out var sv)
                    ? L(U4(id), A(sv.VariableName), A(sv.Units))
                    : L(U4(id), A(""), A(""))))
        };
    }

    private SecsMessage HandleS1F13(SecsMessage msg)
    {
        StopT7Timer();
        SetCommunicationState(CommunicationState.Communicating);

        if (_controlState == ControlState.EquipmentOffline)
            SetControlState(ControlState.AttemptOnline);

        return new SecsMessage(1, 14, "S1F14")
        {
            SecsItem = L(
                B(0),
                L(A(ModelName), A(SoftRev)))
        };
    }

    private SecsMessage HandleS1F15(SecsMessage msg)
    {
        SetControlState(ControlState.HostOffline);
        return new SecsMessage(1, 16, "S1F16") { SecsItem = B(0) };
    }

    private SecsMessage HandleS1F17(SecsMessage msg)
    {
        byte onlack = _controlState == ControlState.OnlineRemote ? (byte)2 : (byte)0;
        SetControlState(ControlState.OnlineRemote);
        return new SecsMessage(1, 18, "S1F18") { SecsItem = B(onlack) };
    }

    // ═════════════════════════════════════════════════════════════
    // Stream 2: Equipment Control & Diagnostics
    // ═════════════════════════════════════════════════════════════

    private SecsMessage HandleS2F13(SecsMessage msg)
    {
        var ecIds = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : EquipmentConstants.Keys.ToList();

        return new SecsMessage(2, 14, "S2F14")
        {
            SecsItem = L(ecIds.Select(id =>
                EquipmentConstants.TryGetValue(id, out var ec)
                    ? BuildTypedItem(ec.Format, ec.CurrentValue)
                    : L()))
        };
    }

    private SecsMessage HandleS2F15(SecsMessage msg)
    {
        byte eac = 0;
        if (msg.SecsItem != null)
        {
            foreach (var item in msg.SecsItem)
            {
                if (item.Count < 2) continue;
                var ecid = item[0].FirstValue<uint>();
                if (EquipmentConstants.TryGetValue(ecid, out var ec))
                    ec.CurrentValue = item[1].FirstValue<uint>(); // 簡易実装
                else
                { eac = 1; break; }
            }
        }
        return new SecsMessage(2, 16, "S2F16") { SecsItem = B(eac) };
    }

    private SecsMessage HandleS2F17(SecsMessage msg) =>
        new SecsMessage(2, 18, "S2F18")
        {
            SecsItem = A(DateTime.Now.ToString("yyyyMMddHHmmss"))
        };

    private SecsMessage HandleS2F23(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 4)
        {
            var trid   = msg.SecsItem[0].FirstValue<uint>();
            var dsper  = msg.SecsItem[1].FirstValue<uint>();
            var totsmp = msg.SecsItem[2].FirstValue<uint>();
            var svIds  = msg.SecsItem[3].Select(i => i.FirstValue<uint>()).ToList();
            TraceRequests[trid] = new TraceDefinition(trid, dsper, totsmp, svIds) { IsActive = true };
        }
        return new SecsMessage(2, 24, "S2F24") { SecsItem = B(0) };
    }

    private SecsMessage HandleS2F29(SecsMessage msg)
    {
        var ecIds = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : EquipmentConstants.Keys.ToList();

        return new SecsMessage(2, 30, "S2F30")
        {
            SecsItem = L(ecIds.Select(id =>
                EquipmentConstants.TryGetValue(id, out var ec)
                    ? L(U4(id),
                        A(ec.ConstantName),
                        BuildTypedItem(ec.Format, ec.MinValue),
                        BuildTypedItem(ec.Format, ec.MaxValue),
                        BuildTypedItem(ec.Format, ec.DefaultValue),
                        A(ec.Units))
                    : L(U4(id), A(""), L(), L(), L(), A(""))))
        };
    }

    private SecsMessage HandleS2F31(SecsMessage msg) =>
        new SecsMessage(2, 32, "S2F32") { SecsItem = B(0) };

    private SecsMessage HandleS2F33(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var rptList = msg.SecsItem[1];
            if (rptList.Count == 0)
            {
                Reports.Clear();
            }
            else
            {
                foreach (var rptItem in rptList)
                {
                    if (rptItem.Count < 2) continue;
                    var rptId  = rptItem[0].FirstValue<uint>();
                    var vidList = rptItem[1].Select(i => i.FirstValue<uint>()).ToList();
                    if (vidList.Count == 0) Reports.Remove(rptId);
                    else Reports[rptId] = new ReportDefinition(rptId, vidList);
                }
            }
        }
        return new SecsMessage(2, 34, "S2F34") { SecsItem = B(0) };
    }

    private SecsMessage HandleS2F35(SecsMessage msg)
    {
        byte lrack = 0;
        if (msg.SecsItem?.Count >= 2)
        {
            foreach (var ceLink in msg.SecsItem[1])
            {
                if (ceLink.Count < 2) continue;
                var ceid   = ceLink[0].FirstValue<uint>();
                var rptIds = ceLink[1].Select(i => i.FirstValue<uint>()).ToList();
                if (!CollectionEvents.ContainsKey(ceid)) { lrack = 4; break; }
                EventReportLinks[ceid] = rptIds;
            }
        }
        return new SecsMessage(2, 36, "S2F36") { SecsItem = B(lrack) };
    }

    private SecsMessage HandleS2F37(SecsMessage msg)
    {
        byte erack = 0;
        if (msg.SecsItem?.Count >= 2)
        {
            var enable   = msg.SecsItem[0].FirstValue<byte>() != 0;
            var ceidList = msg.SecsItem[1].Select(i => i.FirstValue<uint>()).ToList();

            if (ceidList.Count == 0)
                foreach (var k in EnabledEvents.Keys.ToList()) EnabledEvents[k] = enable;
            else
                foreach (var ceid in ceidList)
                {
                    if (!CollectionEvents.ContainsKey(ceid)) { erack = 1; break; }
                    EnabledEvents[ceid] = enable;
                }
        }
        return new SecsMessage(2, 38, "S2F38") { SecsItem = B(erack) };
    }

    private SecsMessage HandleS2F41(SecsMessage msg)
    {
        var cmdName = msg.SecsItem?.Count >= 1 ? msg.SecsItem[0].GetString() : "";
        Log($"[CMD] ホストコマンド受信: {cmdName}");

        byte hcack = cmdName.ToUpperInvariant() switch
        {
            "START"  => StartProcess(),
            "STOP"   => StopProcess(),
            "PAUSE"  => PauseProcess(),
            "RESUME" => ResumeProcess(),
            "LOCAL"  => SetLocal(),
            "REMOTE" => SetRemote(),
            _        => 1,
        };

        _ = SendCollectionEventAsync(7u); // CommandInitiated
        return new SecsMessage(2, 42, "S2F42")
        {
            SecsItem = L(B(hcack), L())
        };
    }

    private byte StartProcess()
    {
        if (_processingState is ProcessingState.Ready or ProcessingState.Idle)
        {
            SetProcessingState(ProcessingState.Executing);
            _ = SendCollectionEventAsync(101u);
            return 0;
        }
        return 3;
    }

    private byte StopProcess()
    {
        if (_processingState is ProcessingState.Executing or ProcessingState.Pause)
        {
            SetProcessingState(ProcessingState.Idle);
            _ = SendCollectionEventAsync(102u);
            return 0;
        }
        return 3;
    }

    private byte PauseProcess()
    {
        if (_processingState == ProcessingState.Executing)
        { SetProcessingState(ProcessingState.Pause); return 0; }
        return 3;
    }

    private byte ResumeProcess()
    {
        if (_processingState == ProcessingState.Pause)
        { SetProcessingState(ProcessingState.Executing); return 0; }
        return 3;
    }

    private byte SetLocal()  { SetControlState(ControlState.OnlineLocal);  return 0; }
    private byte SetRemote() { SetControlState(ControlState.OnlineRemote); return 0; }

    private SecsMessage HandleS2F45(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            foreach (var limitItem in msg.SecsItem[1])
            {
                if (limitItem.Count < 2) continue;
                var vid = limitItem[0].FirstValue<uint>();
                var vla = new VariableLimitAttribute(vid);
                uint idx = 0;
                foreach (var lp in limitItem[1])
                {
                    var pair = new LimitPair { LimitId = idx++ };
                    if (lp.Count >= 2)
                    {
                        pair.UpperCollectionEventId = lp[0].FirstValue<uint>();
                        pair.LowerCollectionEventId = lp[1].FirstValue<uint>();
                    }
                    vla.Limits.Add(pair);
                }
                VariableLimits[vid] = vla;
            }
        }
        return new SecsMessage(2, 46, "S2F46") { SecsItem = B(0) };
    }

    private SecsMessage HandleS2F47(SecsMessage msg)
    {
        var vids = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : VariableLimits.Keys.ToList();

        return new SecsMessage(2, 48, "S2F48")
        {
            SecsItem = L(vids.Select(vid =>
                VariableLimits.TryGetValue(vid, out var vla)
                    ? L(U4(vid), B(0), L(vla.Limits.Select(lp =>
                        L(U4(lp.UpperCollectionEventId), U4(lp.LowerCollectionEventId)))))
                    : L(U4(vid), B(1), L())))
        };
    }

    // ═════════════════════════════════════════════════════════════
    // Stream 5: Exception Handling
    // ═════════════════════════════════════════════════════════════

    private SecsMessage HandleS5F3(SecsMessage msg)
    {
        byte aeack = 0;
        if (msg.SecsItem?.Count >= 2)
        {
            var enable = msg.SecsItem[0].FirstValue<byte>() != 0;
            var alids  = msg.SecsItem[1].Select(i => i.FirstValue<uint>()).ToList();
            if (alids.Count == 0)
                foreach (var a in Alarms.Values) a.IsEnabled = enable;
            else
                foreach (var alid in alids)
                {
                    if (!Alarms.TryGetValue(alid, out var alarm)) { aeack = 1; break; }
                    alarm.IsEnabled = enable;
                }
        }
        return new SecsMessage(5, 4, "S5F4") { SecsItem = B(aeack) };
    }

    private SecsMessage HandleS5F5(SecsMessage msg)
    {
        var alids = msg.SecsItem is { Count: > 0 }
            ? msg.SecsItem.Select(i => i.FirstValue<uint>()).ToList()
            : Alarms.Keys.ToList();

        return new SecsMessage(5, 6, "S5F6")
        {
            SecsItem = L(alids.Select(id =>
                Alarms.TryGetValue(id, out var a)
                    ? L(B((byte)a.Category), U4(a.AlarmId), A(a.AlarmText))
                    : L(B(0), U4(id), A(""))))
        };
    }

    private SecsMessage HandleS5F7(SecsMessage msg) =>
        new SecsMessage(5, 8, "S5F8")
        {
            SecsItem = L(Alarms.Values.Where(a => a.IsEnabled)
                .Select(a => L(B((byte)a.Category), U4(a.AlarmId), A(a.AlarmText))))
        };

    // ═════════════════════════════════════════════════════════════
    // Stream 6: Data Collection
    // ═════════════════════════════════════════════════════════════

    private SecsMessage HandleS6F15(SecsMessage msg)
    {
        var ceid = msg.SecsItem?.FirstValue<uint>() ?? 0;
        var reportItems = BuildReportItems(ceid);
        return new SecsMessage(6, 16, "S6F16")
        {
            SecsItem = L(U4(0), U4(ceid), L(reportItems))
        };
    }

    private SecsMessage HandleS6F17(SecsMessage msg)
    {
        var ceid = msg.SecsItem?.FirstValue<uint>() ?? 0;
        var reportItems = new List<Item>();
        if (EventReportLinks.TryGetValue(ceid, out var rptIds))
        {
            foreach (var rptId in rptIds)
            {
                if (!Reports.TryGetValue(rptId, out var rpt)) continue;
                var varItems = rpt.VariableIds.Select(vid =>
                {
                    string name = StatusVariables.TryGetValue(vid, out var sv) ? sv.VariableName
                                : DataVariables.TryGetValue(vid, out var dv) ? dv.VariableName : "";
                    return L(U4(vid), A(name), BuildVariableValueItem(vid));
                }).ToList();
                reportItems.Add(L(U4(rptId), L(varItems)));
            }
        }
        return new SecsMessage(6, 18, "S6F18")
        {
            SecsItem = L(U4(0), U4(ceid), L(reportItems))
        };
    }

    private SecsMessage HandleS6F19(SecsMessage msg)
    {
        var rptId = msg.SecsItem?.FirstValue<uint>() ?? 0;
        var items = Reports.TryGetValue(rptId, out var rpt)
            ? rpt.VariableIds.Select(vid => BuildVariableValueItem(vid)).ToList()
            : new List<Item>();
        return new SecsMessage(6, 20, "S6F20") { SecsItem = L(items) };
    }

    private List<Item> BuildReportItems(uint ceid)
    {
        var items = new List<Item>();
        if (!EventReportLinks.TryGetValue(ceid, out var rptIds)) return items;
        foreach (var rptId in rptIds)
        {
            if (!Reports.TryGetValue(rptId, out var rpt)) continue;
            items.Add(L(U4(rptId), L(rpt.VariableIds.Select(vid => BuildVariableValueItem(vid)))));
        }
        return items;
    }

    // ═════════════════════════════════════════════════════════════
    // Stream 10: Terminal Services
    // ═════════════════════════════════════════════════════════════

    private SecsMessage HandleS10F3(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var tid  = msg.SecsItem[0].FirstValue<byte>();
            var text = msg.SecsItem[1].GetString();
            Log($"[TERMINAL TID={tid}] {text}");
        }
        return new SecsMessage(10, 4, "S10F4") { SecsItem = B(0) };
    }

    private SecsMessage HandleS10F5(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var tid = msg.SecsItem[0].FirstValue<byte>();
            for (int i = 1; i < msg.SecsItem.Count; i++)
                Log($"[TERMINAL TID={tid}] {msg.SecsItem[i].GetString()}");
        }
        return new SecsMessage(10, 6, "S10F6") { SecsItem = B(0) };
    }

    // ═════════════════════════════════════════════════════════════
    // 不明メッセージ → S9F5
    // ═════════════════════════════════════════════════════════════
    private SecsMessage HandleUnknown(SecsMessage msg)
    {
        Log($"[WARN] 未実装 S{msg.S}F{msg.F}");
        return new SecsMessage(9, 5, "S9F5") { SecsItem = B(msg.S, msg.F) };
    }

    // ─────────────────────────────────────────────────────────────
    // ログ
    // ─────────────────────────────────────────────────────────────
    private void Log(string message)
    {
        _logger.LogInformation("{Message}", message);
        MessageLogged?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}
