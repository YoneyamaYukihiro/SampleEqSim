using Microsoft.Extensions.Logging;
using Secs4Net;
using static Secs4Net.Item;

namespace SampleEqSim.Core.Gem;

/// <summary>
/// GEM Full Compliant 陬・ｽｮ繝｢繝・Ν (SEMI E30)
/// HSMS騾壻ｿ｡繧剃ｻ九＠縺ｦGEM貅匁侠縺ｮ陬・ｽｮ蜍穂ｽ懊ｒ繧ｷ繝溘Η繝ｬ繝ｼ繝医☆繧・
/// </summary>
public class GemEquipmentModel
{
    private readonly ISecsGem _secsGem;
    private readonly ILogger<GemEquipmentModel> _logger;

    // 笏笏笏 State Machines 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private CommunicationState _communicationState = CommunicationState.Disabled;
    private ControlState _controlState = ControlState.EquipmentOffline;
    private ProcessingState _processingState = ProcessingState.Init;

    // 笏笏笏 GEM Data Model 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public Dictionary<uint, AlarmDefinition> Alarms { get; } = new();
    public Dictionary<uint, CollectionEventDefinition> CollectionEvents { get; } = new();
    public Dictionary<uint, StatusVariable> StatusVariables { get; } = new();
    public Dictionary<uint, DataVariable> DataVariables { get; } = new();
    public Dictionary<uint, EquipmentConstant> EquipmentConstants { get; } = new();
    public Dictionary<uint, ReportDefinition> Reports { get; } = new();
    /// <summary>繧､繝吶Φ繝・竊・繝ｬ繝昴・繝・D 縺ｮ繝ｪ繝ｳ繧ｯ繝・・繝悶Ν</summary>
    public Dictionary<uint, List<uint>> EventReportLinks { get; } = new();
    /// <summary>繧､繝吶Φ繝医・譛牙柑/辟｡蜉ｹ</summary>
    public Dictionary<uint, bool> EnabledEvents { get; } = new();
    public Dictionary<uint, TraceDefinition> TraceRequests { get; } = new();
    public Dictionary<uint, VariableLimitAttribute> VariableLimits { get; } = new();

    // 笏笏笏 Equipment Info 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public string ModelName { get; set; } = "SampleEquipment";
    public string SoftRev { get; set; } = "1.0.0";

    // 笏笏笏 Events 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public event EventHandler<CommunicationState>? CommunicationStateChanged;
    public event EventHandler<ControlState>? ControlStateChanged;
    public event EventHandler<ProcessingState>? ProcessingStateChanged;
    public event EventHandler<string>? MessageLogged;
    public event EventHandler<(uint AlarmId, bool IsSet)>? AlarmStateChanged;

    // 笏笏笏 Properties 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public CommunicationState CommunicationState => _communicationState;
    public ControlState ControlState => _controlState;
    public ProcessingState ProcessingState => _processingState;

    // T7繧ｿ繧､繝槭・: WAIT_CR_FROM_HOST 縺ｧ縺ｮ蠕・ｩ溘ち繧､繝繧｢繧ｦ繝・
    private System.Timers.Timer? _t7Timer;

    public GemEquipmentModel(ISecsGem secsGem, ILogger<GemEquipmentModel> logger)
    {
        _secsGem = secsGem;
        _logger = logger;

        InitializeGemData();
        RegisterMessageHandlers();
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 蛻晄悄蛹・
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void InitializeGemData()
    {
        // 笏笏 Status Variables (SVID) 笏笏
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

        // 笏笏 Data Variables (DVID) 笏笏
        DataVariables[1001] = new DataVariable(1001, "ProcessTemp", "F4", () =>
            _temperature, "degC");
        DataVariables[1002] = new DataVariable(1002, "ProcessPressure", "F4", () =>
            _pressure, "kPa");
        DataVariables[1003] = new DataVariable(1003, "ProcessTime", "U4", () =>
            (uint)_processElapsedSeconds, "sec");

        // 笏笏 Equipment Constants (ECID) 笏笏
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

        // 笏笏 Collection Events (CEID) 笏笏
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

        // 笏笏 Alarms (ALID) 笏笏
        Alarms[1] = new AlarmDefinition(1, "LOW_AIR", "菴主悸邵ｮ遨ｺ豌玲､懷・", AlarmCategory.Fault);
        Alarms[2] = new AlarmDefinition(2, "HIGH_TEMP", "鬮俶ｸｩ讀懷・", AlarmCategory.Warning);
        Alarms[3] = new AlarmDefinition(3, "DOOR_OPEN", "Door is open", AlarmCategory.Fault);
        Alarms[4] = new AlarmDefinition(4, "POWER_FAIL", "髮ｻ貅千焚蟶ｸ", AlarmCategory.Equipment);
        Alarms[5] = new AlarmDefinition(5, "COMM_ERROR", "騾壻ｿ｡繧ｨ繝ｩ繝ｼ", AlarmCategory.Warning);
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｷ繝溘Η繝ｬ繝ｼ繧ｷ繝ｧ繝ｳ逕ｨ繝・・繧ｿ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private float _temperature = 25.0f;
    private float _pressure = 101.3f;
    private string _lotId = "";
    private string _recipeId = "";
    private uint _processElapsedSeconds = 0;

    public void SetTemperature(float value) => _temperature = value;
    public void SetPressure(float value) => _pressure = value;
    public void SetLotId(string value) => _lotId = value;
    public void SetRecipeId(string value) => _recipeId = value;

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 謗･邯夂憾諷句､牙喧
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void OnConnectionChanged(object? sender, ConnectionState state)
    {
        Log($"[HSMS] 謗･邯夂憾諷句､牙喧: {state}");
        if (state == ConnectionState.Connected)
        {
            SetCommunicationState(CommunicationState.WaitCrFromHost);
            StartT7Timer();
        }
        else if (state != ConnectionState.Selected)
        {
            StopT7Timer();
            SetCommunicationState(CommunicationState.Disabled);
        }
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // T7 繧ｿ繧､繝槭・ (騾壻ｿ｡遒ｺ遶九ち繧､繝繧｢繧ｦ繝・
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void StartT7Timer()
    {
        var timeout = Convert.ToDouble(EquipmentConstants[1].CurrentValue) * 1000;
        _t7Timer?.Dispose();
        _t7Timer = new System.Timers.Timer(timeout);
        _t7Timer.Elapsed += (s, e) =>
        {
            _t7Timer?.Dispose();
            Log("[T7] Timeout while waiting for establish communications");
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

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 迥ｶ諷矩・遘ｻ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void SetCommunicationState(CommunicationState newState)
    {
        if (_communicationState == newState) return;
        Log($"[ComState] {_communicationState} 竊・{newState}");
        _communicationState = newState;
        CommunicationStateChanged?.Invoke(this, newState);
    }

    private void SetControlState(ControlState newState)
    {
        if (_controlState == newState) return;
        Log($"[CtrlState] {_controlState} 竊・{newState}");
        _controlState = newState;
        ControlStateChanged?.Invoke(this, newState);

        // 蛻ｶ蠕｡迥ｶ諷句､牙喧繧､繝吶Φ繝医ｒ騾∽ｿ｡
        _ = SendCollectionEventAsync(newState switch
        {
            ControlState.EquipmentOffline => 1u,
            ControlState.OnlineLocal => 2u,
            ControlState.OnlineRemote => 3u,
            _ => 0u
        });
    }

    public void SetProcessingState(ProcessingState newState)
    {
        if (_processingState == newState) return;
        Log($"[ProcState] {_processingState} 竊・{newState}");
        _processingState = newState;
        ProcessingStateChanged?.Invoke(this, newState);
        _ = SendCollectionEventAsync(4u); // ProcessingStateChange
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧｢繝ｩ繝ｼ繝謫堺ｽ・
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public async Task SetAlarmAsync(uint alarmId, bool set)
    {
        if (!Alarms.TryGetValue(alarmId, out var alarm)) return;
        if (alarm.IsSet == set) return;

        alarm.IsSet = set;
        AlarmStateChanged?.Invoke(this, (alarmId, set));

        if (alarm.IsEnabled && _communicationState == CommunicationState.Communicating)
        {
            // S5F1: Alarm Report Send
            var msg = new SecsMessage(5, 1)
            {
                SecsItem = L(
                    B((byte)(set ? 0x81 : 0x01)),  // ALCD: bit7=set, bit0=personal
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
                Log($"[ERR] S5F1 騾∽ｿ｡螟ｱ謨・ {ex.Message}");
            }
        }

        // 繧｢繝ｩ繝ｼ繝繧､繝吶Φ繝医ｒ騾∽ｿ｡
        await SendCollectionEventAsync(set ? 5u : 6u);
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧､繝吶Φ繝医Ξ繝昴・繝磯∽ｿ｡
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public async Task SendCollectionEventAsync(uint ceid)
    {
        if (ceid == 0) return;
        if (_communicationState != CommunicationState.Communicating) return;
        if (!EnabledEvents.TryGetValue(ceid, out var enabled) || !enabled) return;
        if (!CollectionEvents.ContainsKey(ceid)) return;

        // 繝ｪ繝ｳ繧ｯ縺輔ｌ縺溘Ξ繝昴・繝医ｒ蜿朱寔
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

        var s6f11 = new SecsMessage(6, 11)
        {
            SecsItem = L(
                U4(0),          // DATAID
                U4(ceid),       // CEID
                L(reportItems)) // RPT list
        };

        try
        {
            Log($"SND >> S6F11 CEID={ceid} [{CollectionEvents[ceid].EventName}]");
            await _secsGem.SendAsync(s6f11);
        }
        catch (Exception ex)
        {
            Log($"[ERR] S6F11 騾∽ｿ｡螟ｱ謨・ {ex.Message}");
        }
    }

    private Item BuildVariableValueItem(uint vid)
    {
        if (StatusVariables.TryGetValue(vid, out var sv))
            return BuildTypedItem(sv.Format, sv.GetValue());
        if (DataVariables.TryGetValue(vid, out var dv))
            return BuildTypedItem(dv.Format, dv.GetValue());
        if (EquipmentConstants.TryGetValue(vid, out var ec))
            return BuildTypedItem(ec.Format, ec.CurrentValue);
        return A(""); // 荳肴・縺ｪ螟画焚
    }

    private static Item BuildTypedItem(string format, object value)
    {
        return format.ToUpper() switch
        {
            "A" => A(value?.ToString() ?? ""),
            "U1" => U1(Convert.ToByte(value)),
            "U2" => U2(Convert.ToUInt16(value)),
            "U4" => U4(Convert.ToUInt32(value)),
            "I1" => I1(Convert.ToSByte(value)),
            "I2" => I2(Convert.ToInt16(value)),
            "I4" => I4(Convert.ToInt32(value)),
            "F4" => F4(Convert.ToSingle(value)),
            "F8" => F8(Convert.ToDouble(value)),
            "BOOLEAN" or "BOOL" => Boolean(Convert.ToBoolean(value)),
            _ => A(value?.ToString() ?? ""),
        };
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繝｡繝・そ繝ｼ繧ｸ繝上Φ繝峨Λ繝ｼ逋ｻ骭ｲ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void RegisterMessageHandlers()
    {
        _ = Task.Run(ProcessPrimaryMessagesLoopAsync);
    }

    private async Task ProcessPrimaryMessagesLoopAsync()
    {
        while (true)
        {
            try
            {
                await foreach (var wrapper in _secsGem.GetPrimaryMessageAsync())
                {
                    var msg = wrapper.PrimaryMessage;
                    Log($"RCV << S{msg.S}F{msg.F}");

                    SecsMessage? reply = (msg.S, msg.F) switch
                    {
                        (1, 1) => HandleS1F1(msg),
                        (1, 3) => HandleS1F3(msg),
                        (1, 11) => HandleS1F11(msg),
                        (1, 13) => HandleS1F13(msg),
                        (1, 15) => HandleS1F15(msg),
                        (1, 17) => HandleS1F17(msg),
                        (2, 13) => HandleS2F13(msg),
                        (2, 15) => HandleS2F15(msg),
                        (2, 17) => HandleS2F17(msg),
                        (2, 23) => HandleS2F23(msg),
                        (2, 29) => HandleS2F29(msg),
                        (2, 31) => HandleS2F31(msg),
                        (2, 33) => HandleS2F33(msg),
                        (2, 35) => HandleS2F35(msg),
                        (2, 37) => HandleS2F37(msg),
                        (2, 41) => HandleS2F41(msg),
                        (2, 45) => HandleS2F45(msg),
                        (2, 47) => HandleS2F47(msg),
                        (5, 3) => HandleS5F3(msg),
                        (5, 5) => HandleS5F5(msg),
                        (5, 7) => HandleS5F7(msg),
                        (6, 15) => HandleS6F15(msg),
                        (6, 17) => HandleS6F17(msg),
                        (6, 19) => HandleS6F19(msg),
                        (10, 3) => HandleS10F3(msg),
                        (10, 5) => HandleS10F5(msg),
                        _ => HandleUnknown(msg),
                    };

                    if (msg.ReplyExpected && reply != null)
                    {
                        Log($"SND >> S{reply.S}F{reply.F}");
                        await wrapper.TryReplyAsync(reply);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Message processing error");
            }
        }
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // Stream 1: Equipment Status
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

    /// <summary>S1F1 Are You There 竊・S1F2 On Line Data</summary>
    private SecsMessage HandleS1F1(SecsMessage msg)
    {
        return new SecsMessage(1, 2)
        {
            SecsItem = L(
                A(ModelName),
                A(SoftRev))
        };
    }

    /// <summary>S1F3 Selected Equipment Status Request 竊・S1F4 Selected Equipment Status Data</summary>
    private SecsMessage HandleS1F3(SecsMessage msg)
    {
        var svIds = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : StatusVariables.Keys.ToList();

        var items = svIds.Select(svid =>
        {
            if (StatusVariables.TryGetValue(svid, out var sv))
                return BuildTypedItem(sv.Format, sv.GetValue());
            return L(); // 荳肴・縺ｪSVID
        }).ToList();

        return new SecsMessage(1, 4)
        {
            SecsItem = L(items)
        };
    }

    /// <summary>S1F11 Status Variable Namelist Request 竊・S1F12 Status Variable Namelist Reply</summary>
    private SecsMessage HandleS1F11(SecsMessage msg)
    {
        var svIds = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : StatusVariables.Keys.ToList();

        var items = svIds.Select(svid =>
        {
            if (StatusVariables.TryGetValue(svid, out var sv))
                return L(U4(svid), A(sv.VariableName), A(sv.Units));
            return L(U4(svid), A(""), A(""));
        }).ToList();

        return new SecsMessage(1, 12)
        {
            SecsItem = L(items)
        };
    }

    /// <summary>S1F13 Establish Communications Request 竊・S1F14 Establish Communications Acknowledge</summary>
    private SecsMessage HandleS1F13(SecsMessage msg)
    {
        StopT7Timer();
        SetCommunicationState(CommunicationState.Communicating);

        // 蛻ｶ蠕｡迥ｶ諷九ｒAttemptOnline縺ｫ遘ｻ陦・
        if (_controlState == ControlState.EquipmentOffline)
            SetControlState(ControlState.AttemptOnline);

        return new SecsMessage(1, 14)
        {
            SecsItem = L(
                B(0),           // COMMACK: 0=accepted
                L(
                    A(ModelName),
                    A(SoftRev)))
        };
    }

    /// <summary>S1F15 Request OFF-LINE 竊・S1F16 OFF-LINE Acknowledge</summary>
    private SecsMessage HandleS1F15(SecsMessage msg)
    {
        SetControlState(ControlState.HostOffline);
        return new SecsMessage(1, 16)
        {
            SecsItem = B(0) // OFLACK: 0=ACK
        };
    }

    /// <summary>S1F17 Request ON-LINE 竊・S1F18 ON-LINE Acknowledge</summary>
    private SecsMessage HandleS1F17(SecsMessage msg)
    {
        byte onlack = 0; // 0=OK
        if (_controlState == ControlState.OnlineRemote)
            onlack = 2; // 2=Already ON-LINE

        SetControlState(ControlState.OnlineRemote);
        return new SecsMessage(1, 18)
        {
            SecsItem = B(onlack)
        };
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // Stream 2: Equipment Control & Diagnostics
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

    /// <summary>S2F13 Equipment Constant Request 竊・S2F14 Equipment Constant Data</summary>
    private SecsMessage HandleS2F13(SecsMessage msg)
    {
        var ecIds = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : EquipmentConstants.Keys.ToList();

        var items = ecIds.Select(ecid =>
        {
            if (EquipmentConstants.TryGetValue(ecid, out var ec))
                return BuildTypedItem(ec.Format, ec.CurrentValue);
            return L();
        }).ToList();

        return new SecsMessage(2, 14)
        {
            SecsItem = L(items)
        };
    }

    /// <summary>S2F15 New Equipment Constant Send 竊・S2F16 New Equipment Constant Acknowledge</summary>
    private SecsMessage HandleS2F15(SecsMessage msg)
    {
        byte eac = 0; // 0=ACK
        if (msg.SecsItem != null)
        {
            foreach (var item in msg.SecsItem.Items)
            {
                if (item.Count < 2) continue;
                var ecid = item[0].FirstValue<uint>();
                if (!EquipmentConstants.TryGetValue(ecid, out var ec))
                {
                    eac = 1; // 1=螳壽焚ID荳肴ｭ｣
                    break;
                }
                // 蛟､繧呈峩譁ｰ (邁｡譏灘ｮ溯｣・
                ec.CurrentValue = ec.Format.ToUpperInvariant() switch
                {
                    "A" => item[1].GetString(),
                    "U1" => item[1].FirstValue<byte>(),
                    "U2" => item[1].FirstValue<ushort>(),
                    "U4" => item[1].FirstValue<uint>(),
                    "I1" => item[1].FirstValue<sbyte>(),
                    "I2" => item[1].FirstValue<short>(),
                    "I4" => item[1].FirstValue<int>(),
                    "F4" => item[1].FirstValue<float>(),
                    "F8" => item[1].FirstValue<double>(),
                    "BOOLEAN" or "BOOL" => item[1].FirstValue<byte>() != 0,
                    _ => ec.DefaultValue
                };
            }
        }
        return new SecsMessage(2, 16)
        {
            SecsItem = B(eac)
        };
    }

    /// <summary>S2F17 Date and Time Request 竊・S2F18 Date and Time Data</summary>
    private SecsMessage HandleS2F17(SecsMessage msg)
    {
        return new SecsMessage(2, 18)
        {
            SecsItem = A(DateTime.Now.ToString("yyyyMMddHHmmss"))
        };
    }

    /// <summary>S2F23 Trace Initialize Send 竊・S2F24 Trace Initialize Acknowledge</summary>
    private SecsMessage HandleS2F23(SecsMessage msg)
    {
        byte tiaack = 0; // 0=ACK
        if (msg.SecsItem?.Count >= 4)
        {
            var trid = msg.SecsItem[0].FirstValue<uint>();
            var dsper = msg.SecsItem[1].FirstValue<uint>();
            var totsmp = msg.SecsItem[2].FirstValue<uint>();
            var svIds = msg.SecsItem[3].Items.Select(i => i.FirstValue<uint>()).ToList();

            TraceRequests[trid] = new TraceDefinition(trid, dsper, totsmp, svIds)
            {
                IsActive = true
            };
        }
        return new SecsMessage(2, 24)
        {
            SecsItem = B(tiaack)
        };
    }

    /// <summary>S2F29 Equipment Constant Namelist Request 竊・S2F30 Equipment Constant Namelist</summary>
    private SecsMessage HandleS2F29(SecsMessage msg)
    {
        var ecIds = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : EquipmentConstants.Keys.ToList();

        var items = ecIds.Select(ecid =>
        {
            if (EquipmentConstants.TryGetValue(ecid, out var ec))
                return L(
                    U4(ecid),
                    A(ec.ConstantName),
                    BuildTypedItem(ec.Format, ec.MinValue),
                    BuildTypedItem(ec.Format, ec.MaxValue),
                    BuildTypedItem(ec.Format, ec.DefaultValue),
                    A(ec.Units));
            return L(U4(ecid), A(""), L(), L(), L(), A(""));
        }).ToList();

        return new SecsMessage(2, 30)
        {
            SecsItem = L(items)
        };
    }

    /// <summary>S2F31 Date and Time Set Request 竊・S2F32 Date and Time Set Acknowledge</summary>
    private SecsMessage HandleS2F31(SecsMessage msg)
    {
        // 螳滄圀縺ｮ陬・ｽｮ縺ｧ縺ｯ譎ょ綾繧定ｨｭ螳壹☆繧九′縲√す繝溘Η繝ｬ繝ｼ繧ｿ繝ｼ縺ｧ縺ｯ蜿励￠莉倥￠縺ｮ縺ｿ
        return new SecsMessage(2, 32)
        {
            SecsItem = B(0) // TIACK: 0=ACK
        };
    }

    /// <summary>S2F33 Define Report 竊・S2F34 Define Report Acknowledge</summary>
    private SecsMessage HandleS2F33(SecsMessage msg)
    {
        byte drack = 0; // 0=ACK
        if (msg.SecsItem?.Count >= 2)
        {
            // DATAID 縺ｯ辟｡隕・
            var rptList = msg.SecsItem[1];
            if (rptList.Count == 0)
            {
                // 蜈ｨ繝ｬ繝昴・繝医ｒ蜑企勁
                Reports.Clear();
            }
            else
            {
                foreach (var rptItem in rptList.Items)
                {
                    if (rptItem.Count < 2) continue;
                    var rptId = rptItem[0].FirstValue<uint>();
                    var vidList = rptItem[1].Items.Select(i => i.FirstValue<uint>()).ToList();
                    if (vidList.Count == 0)
                        Reports.Remove(rptId);
                    else
                        Reports[rptId] = new ReportDefinition(rptId, vidList);
                }
            }
        }
        return new SecsMessage(2, 34)
        {
            SecsItem = B(drack)
        };
    }

    /// <summary>S2F35 Link Event Report 竊・S2F36 Link Event Report Acknowledge</summary>
    private SecsMessage HandleS2F35(SecsMessage msg)
    {
        byte lrack = 0; // 0=ACK
        if (msg.SecsItem?.Count >= 2)
        {
            var ceLinkList = msg.SecsItem[1];
            foreach (var ceLink in ceLinkList.Items)
            {
                if (ceLink.Count < 2) continue;
                var ceid = ceLink[0].FirstValue<uint>();
                var rptIds = ceLink[1].Items.Select(i => i.FirstValue<uint>()).ToList();
                if (!CollectionEvents.ContainsKey(ceid)) { lrack = 4; break; }
                EventReportLinks[ceid] = rptIds;
            }
        }
        return new SecsMessage(2, 36)
        {
            SecsItem = B(lrack)
        };
    }

    /// <summary>S2F37 Enable/Disable Event Report 竊・S2F38 Enable/Disable Event Report Acknowledge</summary>
    private SecsMessage HandleS2F37(SecsMessage msg)
    {
        byte erack = 0; // 0=ACK
        if (msg.SecsItem?.Count >= 2)
        {
            var enable = msg.SecsItem[0].FirstValue<byte>() != 0;
            var ceidList = msg.SecsItem[1].Items.Select(i => i.FirstValue<uint>()).ToList();

            if (ceidList.Count == 0)
            {
                // 蜈ｨ繧､繝吶Φ繝医↓驕ｩ逕ｨ
                foreach (var ceid in EnabledEvents.Keys.ToList())
                    EnabledEvents[ceid] = enable;
            }
            else
            {
                foreach (var ceid in ceidList)
                {
                    if (!CollectionEvents.ContainsKey(ceid)) { erack = 1; break; }
                    EnabledEvents[ceid] = enable;
                }
            }
        }
        return new SecsMessage(2, 38)
        {
            SecsItem = B(erack)
        };
    }

    /// <summary>S2F41 Host Command Send 竊・S2F42 Host Command Acknowledge</summary>
    private SecsMessage HandleS2F41(SecsMessage msg)
    {
        byte hcack = 0; // 0=ACK
        string cmdName = "";
        if (msg.SecsItem?.Count >= 1)
            cmdName = msg.SecsItem[0].GetString();

        Log($"[CMD] 繝帙せ繝医さ繝槭Φ繝牙女菫｡: {cmdName}");

        hcack = cmdName.ToUpper() switch
        {
            "START" => StartProcess(),
            "STOP" => StopProcess(),
            "PAUSE" => PauseProcess(),
            "RESUME" => ResumeProcess(),
            "LOCAL" => SetLocal(),
            "REMOTE" => SetRemote(),
            _ => (byte)1 // 1=譛ｪ遏･繧ｳ繝槭Φ繝・
        };

        _ = SendCollectionEventAsync(7u); // CommandInitiated

        return new SecsMessage(2, 42)
        {
            SecsItem = L(
                B(hcack),
                L()) // HCPACK list (遨ｺ)
        };
    }

    private byte StartProcess()
    {
        if (_processingState == ProcessingState.Ready || _processingState == ProcessingState.Idle)
        {
            SetProcessingState(ProcessingState.Executing);
            _ = SendCollectionEventAsync(101u); // ProcessStarted
            return 0;
        }
        return 3; // 螳溯｡御ｸ榊庄
    }

    private byte StopProcess()
    {
        if (_processingState is ProcessingState.Executing or ProcessingState.Pause)
        {
            SetProcessingState(ProcessingState.Idle);
            _ = SendCollectionEventAsync(102u); // ProcessCompleted
            return 0;
        }
        return 3;
    }

    private byte PauseProcess()
    {
        if (_processingState == ProcessingState.Executing)
        {
            SetProcessingState(ProcessingState.Pause);
            return 0;
        }
        return 3;
    }

    private byte ResumeProcess()
    {
        if (_processingState == ProcessingState.Pause)
        {
            SetProcessingState(ProcessingState.Executing);
            return 0;
        }
        return 3;
    }

    private byte SetLocal()
    {
        SetControlState(ControlState.OnlineLocal);
        return 0;
    }

    private byte SetRemote()
    {
        SetControlState(ControlState.OnlineRemote);
        return 0;
    }

    /// <summary>S2F45 Define Variable Limit Attributes 竊・S2F46 Acknowledge</summary>
    private SecsMessage HandleS2F45(SecsMessage msg)
    {
        byte vlaack = 0;
        if (msg.SecsItem?.Count >= 2)
        {
            var limitList = msg.SecsItem[1];
            foreach (var limitItem in limitList.Items)
            {
                if (limitItem.Count < 2) continue;
                var vid = limitItem[0].FirstValue<uint>();
                var vla = new VariableLimitAttribute(vid);
                var limits = limitItem[1];
                for (int i = 0; i < limits.Count; i++)
                {
                    var lp = new LimitPair { LimitId = (uint)i };
                    if (limits[i].Count >= 4)
                    {
                        lp.UpperCollectionEventId = limits[i][0].FirstValue<uint>();
                        lp.LowerCollectionEventId = limits[i][1].FirstValue<uint>();
                        // 荳贋ｸ矩剞蛟､
                    }
                    vla.Limits.Add(lp);
                }
                VariableLimits[vid] = vla;
            }
        }
        return new SecsMessage(2, 46) { SecsItem = B(vlaack) };
    }

    /// <summary>S2F47 Variable Limit Attribute Request 竊・S2F48 Variable Limit Attribute Data</summary>
    private SecsMessage HandleS2F47(SecsMessage msg)
    {
        var vids = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : VariableLimits.Keys.ToList();

        var items = vids.Select(vid =>
        {
            if (VariableLimits.TryGetValue(vid, out var vla))
            {
                var limitItems = vla.Limits.Select(lp =>
                    L(U4(lp.UpperCollectionEventId), U4(lp.LowerCollectionEventId))
                ).ToList();
                return L(U4(vid), B(0), L(limitItems));
            }
            return L(U4(vid), B(1), L()); // VLAACK=1: 螟画焚ID荳肴ｭ｣
        }).ToList();

        return new SecsMessage(2, 48) { SecsItem = L(items) };
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // Stream 5: Exception Handling
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

    /// <summary>S5F3 Enable/Disable Alarm Send 竊・S5F4 Enable/Disable Alarm Acknowledge</summary>
    private SecsMessage HandleS5F3(SecsMessage msg)
    {
        byte aeack = 0;
        if (msg.SecsItem?.Count >= 2)
        {
            var enable = msg.SecsItem[0].FirstValue<byte>() != 0;
            var alids = msg.SecsItem[1].Items.Select(i => i.FirstValue<uint>()).ToList();
            if (alids.Count == 0)
            {
                foreach (var alarm in Alarms.Values) alarm.IsEnabled = enable;
            }
            else
            {
                foreach (var alid in alids)
                {
                    if (!Alarms.TryGetValue(alid, out var alarm)) { aeack = 1; break; }
                    alarm.IsEnabled = enable;
                }
            }
        }
        return new SecsMessage(5, 4) { SecsItem = B(aeack) };
    }

    /// <summary>S5F5 List Alarms Request 竊・S5F6 List Alarms Data</summary>
    private SecsMessage HandleS5F5(SecsMessage msg)
    {
        var alids = msg.SecsItem?.Count > 0
            ? msg.SecsItem.Items.Select(i => i.FirstValue<uint>()).ToList()
            : Alarms.Keys.ToList();

        var items = alids.Select(alid =>
        {
            if (Alarms.TryGetValue(alid, out var alarm))
                return L(
                    B((byte)alarm.Category),
                    U4(alarm.AlarmId),
                    A(alarm.AlarmText));
            return L(B(0), U4(alid), A(""));
        }).ToList();

        return new SecsMessage(5, 6) { SecsItem = L(items) };
    }

    /// <summary>S5F7 List Enabled Alarms 竊・S5F8 List Enabled Alarms Data</summary>
    private SecsMessage HandleS5F7(SecsMessage msg)
    {
        var enabledAlarms = Alarms.Values.Where(a => a.IsEnabled).Select(alarm =>
            L(
                B((byte)alarm.Category),
                U4(alarm.AlarmId),
                A(alarm.AlarmText))
        ).ToList();

        return new SecsMessage(5, 8) { SecsItem = L(enabledAlarms) };
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // Stream 6: Data Collection
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

    /// <summary>S6F15 Event Report Request 竊・S6F16 Event Report Data</summary>
    private SecsMessage HandleS6F15(SecsMessage msg)
    {
        var ceid = msg.SecsItem?.FirstValue<uint>() ?? 0;
        var reportItems = new List<Item>();
        if (EventReportLinks.TryGetValue(ceid, out var rptIds))
        {
            foreach (var rptId in rptIds)
            {
                if (!Reports.TryGetValue(rptId, out var rpt)) continue;
                var varItems = rpt.VariableIds.Select(vid => BuildVariableValueItem(vid)).ToList();
                reportItems.Add(L(U4(rptId), L(varItems)));
            }
        }
        return new SecsMessage(6, 16)
        {
            SecsItem = L(U4(0), U4(ceid), L(reportItems))
        };
    }

    /// <summary>S6F17 Annotated Event Report Request 竊・S6F18 Annotated Event Report Data</summary>
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
        return new SecsMessage(6, 18)
        {
            SecsItem = L(U4(0), U4(ceid), L(reportItems))
        };
    }

    /// <summary>S6F19 Individual Report Request 竊・S6F20 Individual Report Data</summary>
    private SecsMessage HandleS6F19(SecsMessage msg)
    {
        var rptId = msg.SecsItem?.FirstValue<uint>() ?? 0;
        var items = new List<Item>();
        if (Reports.TryGetValue(rptId, out var rpt))
            items = rpt.VariableIds.Select(vid => BuildVariableValueItem(vid)).ToList();
        return new SecsMessage(6, 20) { SecsItem = L(items) };
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // Stream 10: Terminal Services
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・

    /// <summary>S10F3 Terminal Display Single 竊・S10F4 Acknowledge</summary>
    private SecsMessage HandleS10F3(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var tid = msg.SecsItem[0].FirstValue<byte>();
            var text = msg.SecsItem[1].GetString();
            Log($"[TERMINAL TID={tid}] {text}");
        }
        return new SecsMessage(10, 4) { SecsItem = B(0) };
    }

    /// <summary>S10F5 Terminal Display Multiple 竊・S10F6 Acknowledge</summary>
    private SecsMessage HandleS10F5(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var tid = msg.SecsItem[0].FirstValue<byte>();
            for (int i = 1; i < msg.SecsItem.Count; i++)
                Log($"[TERMINAL TID={tid}] {msg.SecsItem[i].GetString()}");
        }
        return new SecsMessage(10, 6) { SecsItem = B(0) };
    }

    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    // 荳肴・繝｡繝・そ繝ｼ繧ｸ 竊・S9F5
    // 笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊絶武笊・
    private SecsMessage HandleUnknown(SecsMessage msg)
    {
        Log($"[WARN] 譛ｪ螳溯｣・Γ繝・そ繝ｼ繧ｸ S{msg.S}F{msg.F}");
        // S9F5: Unrecognized Function
        return new SecsMessage(9, 5)
        {
            SecsItem = B(msg.S, msg.F)
        };
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繝ｭ繧ｰ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void Log(string message)
    {
        _logger.LogInformation("{Message}", message);
        MessageLogged?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }
}





