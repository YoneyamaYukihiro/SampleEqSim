using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Secs4Net;
using SampleEqSim.Core.Config;
using SampleEqSim.Core.Sequence;
using SampleEqSim.Host.Services;
using static Secs4Net.Item;

namespace SampleEqSim.Host.ViewModels;

public partial class HostViewModel : ObservableObject
{
    private readonly ISecsGem _secsGem;

    // ── 接続状態 ──────────────────────────────────────────────────
    [ObservableProperty] private string _connectionStateText = "DISCONNECTED";
    [ObservableProperty] private string _connectionLedBrush = "LedGray";
    [ObservableProperty] private bool _isConnected = false;

    // ── 装置情報 ──────────────────────────────────────────────────
    [ObservableProperty] private string _equipmentModel = "-";
    [ObservableProperty] private string _equipmentSoftRev = "-";
    [ObservableProperty] private string _equipmentDateTime = "-";
    [ObservableProperty] private string _equipmentControlState = "-";
    [ObservableProperty] private string _equipmentProcState = "-";

    // ── コマンド入力 ──────────────────────────────────────────────
    [ObservableProperty] private string _hostCommandText = "START";

    // ── レシピ ────────────────────────────────────────────────────
    [ObservableProperty] private string _recipePpId   = "RECIPE001";
    [ObservableProperty] private string _recipeBody   = "STEP1,TEMP=200,TIME=60\nSTEP2,TEMP=150,TIME=30";
    [ObservableProperty] private string _recipeDirectory = "";

    // ── SVIDリクエスト ────────────────────────────────────────────
    [ObservableProperty] private string _requestSvIds = "101,102,103";
    [ObservableProperty] private string _svDataResult = "";

    // ── ポート/キャリア情報 ───────────────────────────────────────
    [ObservableProperty] private string _portCarrierInfo = "";

    // ── ログ ──────────────────────────────────────────────────────
    public ObservableCollection<LogEntry> MessageLog { get; } = new();
    private const int MaxLogLines = 2000;

    // ── イベントログ ──────────────────────────────────────────────
    public ObservableCollection<string> EventLog { get; } = new();

    // ── 時刻 ──────────────────────────────────────────────────────
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    // ── 接続設定 (HSMS) ───────────────────────────────────────────
    [ObservableProperty] private bool   _connIsActive = true;
    [ObservableProperty] private string _connIpAddress = "127.0.0.1";
    [ObservableProperty] private string _connPort      = "5000";
    [ObservableProperty] private string _connDeviceId  = "0";
    /// <summary>ヘッダーに表示する現在の接続設定サマリ</summary>
    [ObservableProperty] private string _connectionSummary = "";

    // ── シーケンス監視 ────────────────────────────────────────────
    private SequenceMonitor _seqMonitor = null!;
    public ObservableCollection<SequenceStepViewModel> SequenceSteps { get; } = new();
    [ObservableProperty] private string _sequenceName = "";
    [ObservableProperty] private string _sequenceStatus = "";
    [ObservableProperty] private string _sequenceStatusBrush = "LedGray";

    private readonly DispatcherTimer _clockTimer;

    public HostViewModel(ISecsGem secsGem, HostGemService hostGemService)
    {
        _secsGem = secsGem;
        LoadConnectionSettings();
        InitSequenceMonitor();

        // HostGemService 経由でイベント購読
        hostGemService.ConnectionChanged += (_, state) =>
            App.Current.Dispatcher.Invoke(() => OnConnectionChanged(state));

        hostGemService.PrimaryMessageReceived += OnPrimaryMessageReceivedAsync;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();
    }

    // ─────────────────────────────────────────────────────────────
    // 接続設定 (appsettings.json への保存 + 再起動で反映)
    // ─────────────────────────────────────────────────────────────
    private void LoadConnectionSettings()
    {
        var s = AppSettingsService.Load();
        ConnIsActive  = s.IsActive;
        ConnIpAddress = s.IpAddress;
        ConnPort      = s.Port.ToString();
        ConnDeviceId  = s.DeviceId.ToString();
        ConnectionSummary = ConnectionSettingsUtil.Summarize(s);
    }

    [RelayCommand]
    private void SaveAndReconnect()
    {
        if (!ConnectionSettingsUtil.TryBuild(
                ConnIsActive, ConnIpAddress, ConnPort, ConnDeviceId,
                out var settings, out var error))
        {
            MessageBox.Show(error, "入力エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(ConnectionSettingsUtil.ConfirmMessage(settings),
                "接続設定の保存と再起動", MessageBoxButton.OKCancel, MessageBoxImage.Question)
            != MessageBoxResult.OK)
            return;

        try { AppSettingsService.Save(settings); }
        catch (Exception ex)
        {
            MessageBox.Show($"設定の保存に失敗しました。\n{ex.Message}",
                "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ConnectionSettingsUtil.StartNewInstance();
        App.Current.Shutdown();
    }

    // ─────────────────────────────────────────────────────────────
    // シーケンス監視 (sequence.json の想定順序と実通信を照合)
    // ─────────────────────────────────────────────────────────────
    private void InitSequenceMonitor()
    {
        _seqMonitor = new SequenceMonitor(SequenceLoader.Load());
        SequenceSteps.Clear();
        foreach (var step in _seqMonitor.Steps)
            SequenceSteps.Add(new SequenceStepViewModel(step.Sf, step.Desc));
        SequenceName = _seqMonitor.Name;
        RefreshSequence();
    }

    /// <summary>送受信したメッセージを監視器に投入 (UIスレッドで実行)。</summary>
    private void ObserveSeq(byte s, byte f)
        => App.Current.Dispatcher.Invoke(() => { _seqMonitor.Observe(s, f); RefreshSequence(); });

    private void RefreshSequence()
    {
        for (int i = 0; i < SequenceSteps.Count && i < _seqMonitor.Steps.Count; i++)
        {
            var m  = _seqMonitor.Steps[i];
            var vm = SequenceSteps[i];
            vm.StatusBrush = m.Status switch
            {
                StepStatus.Done    => "LedGreen",
                StepStatus.Active  => "LedYellow",
                StepStatus.Skipped => "LedRed",
                _                  => "LedGray",
            };
            vm.Detail = m.Status switch
            {
                StepStatus.Done    => $"✓ {m.TimeText}",
                StepStatus.Active  => "← 次に期待",
                StepStatus.Skipped => "⚠ スキップ",
                _                  => "未到達",
            };
        }
        SequenceStatus      = _seqMonitor.Summary;
        SequenceStatusBrush = _seqMonitor.SummaryBrush;
    }

    [RelayCommand] private void ReloadSequence() => InitSequenceMonitor();
    [RelayCommand] private void ResetSequence() { _seqMonitor.Reset(); RefreshSequence(); }

    // ─────────────────────────────────────────────────────────────
    // 接続状態変化
    // ─────────────────────────────────────────────────────────────
    private void OnConnectionChanged(ConnectionState state)
    {
        ConnectionStateText = state.ToString().ToUpperInvariant();
        IsConnected = state is ConnectionState.Connected or ConnectionState.Selected;
        ConnectionLedBrush = state switch
        {
            ConnectionState.Connected or ConnectionState.Selected => "LedGreen",
            ConnectionState.Connecting => "LedYellow",
            _ => "LedGray",
        };
        AddLog($"[HSMS] 接続状態変化: {state}", MsgLevel.System);
    }

    // ─────────────────────────────────────────────────────────────
    // 受信メッセージ処理 (装置からの Primary)
    // ─────────────────────────────────────────────────────────────
    private async Task OnPrimaryMessageReceivedAsync(PrimaryMessageWrapper e)
    {
        var msg = e.PrimaryMessage;
        App.Current.Dispatcher.Invoke(() =>
            AddLog($"RCV << S{msg.S}F{msg.F} ", MsgLevel.Receive));
        ObserveSeq(msg.S, msg.F);

        SecsMessage? reply = (msg.S, msg.F) switch
        {
            (5, 1)  => HandleS5F1(msg),
            (6, 11) => HandleS6F11(msg),
            (10, 1) => HandleS10F1(msg),
            (14, 9) => HandleS14F9(msg),   // CarrierIn notification
            (14, 11)=> HandleS14F11(msg),  // CarrierOut notification
            _       => null,
        };

        if (msg.ReplyExpected && reply != null)
        {
            App.Current.Dispatcher.Invoke(() =>
                AddLog($"SND >> S{reply.S}F{reply.F} {reply.Name}", MsgLevel.Send));
            ObserveSeq(reply.S, reply.F);
            await e.TryReplyAsync(reply);
        }
    }

    private SecsMessage HandleS5F1(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 3)
        {
            var alcd = msg.SecsItem[0].FirstValue<byte>();
            var alid = msg.SecsItem[1].FirstValue<uint>();
            var alTx = msg.SecsItem[2].GetString();
            var isSet = (alcd & 0x80) != 0;
            App.Current.Dispatcher.Invoke(() =>
            {
                var ev = $"[ALARM {(isSet ? "SET" : "CLR")}] ALID={alid} {alTx}";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {ev}");
                AddLog(ev, isSet ? MsgLevel.Alarm : MsgLevel.System);
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return new SecsMessage(5, 2, false) { SecsItem = B(0) };
    }

    private SecsMessage HandleS6F11(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var ceid = msg.SecsItem[1].FirstValue<uint>();
            App.Current.Dispatcher.Invoke(() =>
            {
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [EVENT] CEID={ceid}");
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return new SecsMessage(6, 12, false) { SecsItem = B(0) };
    }

    private SecsMessage HandleS10F1(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var text = msg.SecsItem[1].GetString();
            App.Current.Dispatcher.Invoke(() =>
            {
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [TERMINAL] {text}");
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return new SecsMessage(10, 2, false) { SecsItem = B(0) };
    }

    private SecsMessage? HandleS14F9(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 3)
        {
            var carrierId = msg.SecsItem[0].GetString();
            var portId    = msg.SecsItem[1].FirstValue<uint>();
            var state     = msg.SecsItem[2].FirstValue<byte>();
            App.Current.Dispatcher.Invoke(() =>
            {
                var ev = $"[CARRIER IN] CarrierID={carrierId} Port={portId} State={state}";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {ev}");
                AddLog(ev, MsgLevel.System);
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return msg.ReplyExpected ? new SecsMessage(14, 10, false) { SecsItem = B(0) } : null;
    }

    private SecsMessage? HandleS14F11(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var carrierId = msg.SecsItem[0].GetString();
            var portId    = msg.SecsItem[1].FirstValue<uint>();
            App.Current.Dispatcher.Invoke(() =>
            {
                var ev = $"[CARRIER OUT] CarrierID={carrierId} Port={portId}";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {ev}");
                AddLog(ev, MsgLevel.System);
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return msg.ReplyExpected ? new SecsMessage(14, 12, false) { SecsItem = B(0) } : null;
    }

    // ─────────────────────────────────────────────────────────────
    // S1F1 Are You There
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendS1F1()
    {
        await SendAndLog(new SecsMessage(1, 1, true), reply =>
        {
            if (reply?.SecsItem?.Count >= 2)
            {
                EquipmentModel   = reply.SecsItem[0].GetString();
                EquipmentSoftRev = reply.SecsItem[1].GetString();
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S1F13 Establish Communications
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendS1F13()
    {
        await SendAndLog(
            new SecsMessage(1, 13, true) { SecsItem = L(A("HOST"), A("1.0")) },
            reply =>
            {
                var commack = reply?.SecsItem?[0].FirstValue<byte>() ?? 0xFF;
                AddLog($"  COMMACK={commack} ({(commack == 0 ? "Accepted" : "Rejected")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S1F15 Request OFF-LINE
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendS1F15()
    {
        await SendAndLog(new SecsMessage(1, 15, true), reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  OFLACK={ack}", MsgLevel.System);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S1F17 Request ON-LINE
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendS1F17()
    {
        await SendAndLog(new SecsMessage(1, 17, true), reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  ONLACK={ack} ({(ack == 0 ? "OK" : ack == 2 ? "Already Online" : "Refused")})", MsgLevel.System);
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S2F17 Date and Time Request
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendS2F17()
    {
        await SendAndLog(new SecsMessage(2, 17, true), reply =>
        {
            EquipmentDateTime = reply?.SecsItem?.GetString() ?? "-";
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S1F3 Status Variables Request
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestStatusVariables()
    {
        var ids = RequestSvIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => uint.TryParse(s, out _))
            .Select(uint.Parse)
            .ToList();

        var msg = new SecsMessage(1, 3, true)
        {
            Name = "S1F3", SecsItem = L(ids.Select(id => (Item)U4(id)))
        };

        await SendAndLog(msg, reply =>
        {
            if (reply?.SecsItem != null)
            {
                var lines = reply.SecsItem.Items
                    .Select((item, i) => $"SV[{(i < ids.Count ? ids[i].ToString() : "?")}] = {FormatItem(item)}")
                    .ToList();
                SvDataResult = string.Join("\n", lines);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S5F5 List Alarms
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task ListAlarms()
    {
        await SendAndLog(new SecsMessage(5, 5, true) { SecsItem = L() }, reply =>
        {
            if (reply?.SecsItem == null) return;
            foreach (var alarmItem in Items(reply.SecsItem!))
            {
                if (alarmItem.Count >= 3)
                    AddLog($"  ALARM ALID={alarmItem[1].FirstValue<uint>()}: {alarmItem[2].GetString()}",
                           MsgLevel.System);
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // S2F41 Host Command
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendHostCommand()
    {
        if (string.IsNullOrWhiteSpace(HostCommandText)) return;
        await SendAndLog(
            new SecsMessage(2, 41, true)
            {
                Name = "S2F41", SecsItem = L(A(HostCommandText.ToUpperInvariant()), L())
            },
            reply =>
            {
                var hcack = reply?.SecsItem?.Count >= 1
                    ? reply.SecsItem[0].FirstValue<byte>() : (byte)0xFF;
                AddLog($"  HCACK={hcack} ({(hcack == 0 ? "ACK" : "NACK")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S2F33 Define Report
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DefineReport()
    {
        // RPTID=1: SV 101(Temperature), 102(Pressure), 103(LotId)
        await SendAndLog(
            new SecsMessage(2, 33, true)
            {
                Name = "S2F33", SecsItem = L(
                    U4(1),
                    L(L(U4(1u), L(U4(101u), U4(102u), U4(103u)))))
            },
            reply =>
            {
                var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                AddLog($"  DRACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S2F35 Link Event Report
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task LinkEventReport()
    {
        // CEID=101,102 → RPTID=1
        await SendAndLog(
            new SecsMessage(2, 35, true)
            {
                Name = "S2F35", SecsItem = L(
                    U4(1),
                    L(
                        L(U4(101u), L(U4(1u))),
                        L(U4(102u), L(U4(1u)))))
            },
            reply =>
            {
                var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                AddLog($"  LRACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S2F37 Enable All Events
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task EnableAllEvents()
    {
        await SendAndLog(
            new SecsMessage(2, 37, true)
            {
                Name = "S2F37", SecsItem = L(Boolean(true), L())
            },
            reply =>
            {
                var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                AddLog($"  ERACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S7F1+S7F3 PP Send (Inquire → Send)
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendProcessProgram()
    {
        if (string.IsNullOrWhiteSpace(RecipePpId)) return;

        // S7F1 Inquire
        await SendAndLog(
            new SecsMessage(7, 1, true) { SecsItem = A(RecipePpId) },
            async reply =>
            {
                var ppgnt = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                AddLog($"  PPGNT={ppgnt} ({(ppgnt == 0 ? "Granted" : "Denied")})", MsgLevel.System);
                if (ppgnt != 0) return;

                // S7F3 Send body
                var body = A(RecipeBody);
                await SendAndLog(
                    new SecsMessage(7, 3, true) { SecsItem = L(A(RecipePpId), body) },
                    r2 =>
                    {
                        var ack = r2?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                        AddLog($"  PPACKED={ack} ({(ack == 0 ? "Accepted" : "Rejected")})", MsgLevel.System);
                    });
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S7F17 PP Delete
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteProcessProgram()
    {
        if (string.IsNullOrWhiteSpace(RecipePpId)) return;
        await SendAndLog(
            new SecsMessage(7, 17, true) { SecsItem = L(A(RecipePpId)) },
            reply =>
            {
                var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
                AddLog($"  PPACKED={ack} ({(ack == 0 ? "Deleted" : "Failed")})", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S7F19 PP Directory
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RequestPpDirectory()
    {
        await SendAndLog(
            new SecsMessage(7, 19, true),
            reply =>
            {
                if (reply?.SecsItem == null) { RecipeDirectory = "(empty)"; return; }
                var ids = Items(reply.SecsItem).Select(i => i.GetString()).ToList();
                RecipeDirectory = ids.Count > 0 ? string.Join(", ", ids) : "(empty)";
                AddLog($"  PP Directory: {RecipeDirectory}", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // S14F1 GetAttr (Port/Carrier)
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task GetPortAttr()
    {
        // Object type "Port", no filter, request PortID/PortState/AccessMode/CarrierID
        await SendAndLog(
            new SecsMessage(14, 1, true)
            {
                SecsItem = L(
                    A("Port"),
                    L(),
                    L(A("PortID"), A("PortState"), A("AccessMode"), A("CarrierID")))
            },
            reply =>
            {
                if (reply?.SecsItem == null) { PortCarrierInfo = "(no reply)"; return; }
                var lines = new System.Text.StringBuilder();
                foreach (var objAttr in Items(reply.SecsItem))
                {
                    for (int i = 0; i < objAttr.Count; i += 2)
                    {
                        if (i + 1 < objAttr.Count)
                            lines.AppendLine($"  {objAttr[i].GetString()}={objAttr[i+1].GetString()}");
                    }
                    lines.AppendLine("---");
                }
                PortCarrierInfo = lines.ToString().TrimEnd();
                AddLog($"  Port Attrs:\n{PortCarrierInfo}", MsgLevel.System);
            });
    }

    [RelayCommand]
    private async Task GetCarrierAttr()
    {
        // Object type "Carrier", no filter, request CarrierID/PortID/CarrierState
        await SendAndLog(
            new SecsMessage(14, 1, true)
            {
                SecsItem = L(
                    A("Carrier"),
                    L(),
                    L(A("CarrierID"), A("PortID"), A("CarrierState")))
            },
            reply =>
            {
                if (reply?.SecsItem == null) { PortCarrierInfo = "(no reply)"; return; }
                var lines = new System.Text.StringBuilder();
                foreach (var objAttr in Items(reply.SecsItem))
                {
                    for (int i = 0; i < objAttr.Count; i += 2)
                    {
                        if (i + 1 < objAttr.Count)
                            lines.AppendLine($"  {objAttr[i].GetString()}={objAttr[i+1].GetString()}");
                    }
                    lines.AppendLine("---");
                }
                PortCarrierInfo = lines.ToString().TrimEnd();
                AddLog($"  Carrier Attrs:\n{PortCarrierInfo}", MsgLevel.System);
            });
    }

    // ─────────────────────────────────────────────────────────────
    // ヘルパー: 送信 + ログ
    // ─────────────────────────────────────────────────────────────
    private async Task SendAndLog(SecsMessage msg, Func<SecsMessage?, Task>? onReplyAsync)
    {
        if (!IsConnected) { AddLog("[ERR] 未接続です", MsgLevel.Error); return; }
        try
        {
            AddLog($"SND >> S{msg.S}F{msg.F} ", MsgLevel.Send);
            ObserveSeq(msg.S, msg.F);
            var reply = await _secsGem.SendAsync(msg);
            if (reply != null) { AddLog($"RCV << S{reply.S}F{reply.F} ", MsgLevel.Receive); ObserveSeq(reply.S, reply.F); }
            if (onReplyAsync != null) await onReplyAsync(reply);
        }
        catch (Exception ex) { AddLog($"[ERR] {ex.Message}", MsgLevel.Error); }
    }

    private async Task SendAndLog(SecsMessage msg, Action<SecsMessage?>? onReply = null)
    {
        if (!IsConnected)
        {
            AddLog("[ERR] 未接続です", MsgLevel.Error);
            return;
        }
        try
        {
            AddLog($"SND >> S{msg.S}F{msg.F} ", MsgLevel.Send);
            ObserveSeq(msg.S, msg.F);
            var reply = await _secsGem.SendAsync(msg);
            if (reply != null)
            {
                AddLog($"RCV << S{reply.S}F{reply.F} ", MsgLevel.Receive);
                ObserveSeq(reply.S, reply.F);
            }
            onReply?.Invoke(reply);
        }
        catch (Exception ex)
        {
            AddLog($"[ERR] {ex.Message}", MsgLevel.Error);
        }
    }

    // Item 値の表示用整形 (Secs4Net.Sml の ToSml() は別パッケージのため自前で簡易整形)
    private static string FormatItem(Item item) => item.Format switch
    {
        SecsFormat.List    => $"<L[{item.Count}]>",
        SecsFormat.ASCII
        or SecsFormat.JIS8 => item.GetString(),
        SecsFormat.Boolean => item.Count > 0 ? item.FirstValue<bool>().ToString()   : "",
        SecsFormat.Binary  => item.Count > 0 ? item.FirstValue<byte>().ToString()   : "",
        SecsFormat.U1      => item.Count > 0 ? item.FirstValue<byte>().ToString()   : "",
        SecsFormat.U2      => item.Count > 0 ? item.FirstValue<ushort>().ToString() : "",
        SecsFormat.U4      => item.Count > 0 ? item.FirstValue<uint>().ToString()   : "",
        SecsFormat.U8      => item.Count > 0 ? item.FirstValue<ulong>().ToString()  : "",
        SecsFormat.I1      => item.Count > 0 ? item.FirstValue<sbyte>().ToString()  : "",
        SecsFormat.I2      => item.Count > 0 ? item.FirstValue<short>().ToString()  : "",
        SecsFormat.I4      => item.Count > 0 ? item.FirstValue<int>().ToString()    : "",
        SecsFormat.I8      => item.Count > 0 ? item.FirstValue<long>().ToString()   : "",
        SecsFormat.F4      => item.Count > 0 ? item.FirstValue<float>().ToString()  : "",
        SecsFormat.F8      => item.Count > 0 ? item.FirstValue<double>().ToString() : "",
        _                  => "",
    };

    private void AddLog(string message, MsgLevel level = MsgLevel.System)
    {
        var entry = new LogEntry(DateTime.Now, message, level);
        MessageLog.Insert(0, entry);
        while (MessageLog.Count > MaxLogLines)
            MessageLog.RemoveAt(MessageLog.Count - 1);
    }

    [RelayCommand] private void ClearLog()      => MessageLog.Clear();
    [RelayCommand] private void ClearEventLog() => EventLog.Clear();

    private static IEnumerable<Item> Items(Item item)
    {
        for (int i = 0; i < item.Count; i++)
            yield return item[i];
    }
}

// ─────────────────────────────────────────────────────────────
// ログエントリ
// ─────────────────────────────────────────────────────────────
public enum MsgLevel { System, Send, Receive, Alarm, Error }

public class LogEntry
{
    public DateTime Time    { get; }
    public string   Message { get; }
    public MsgLevel Level   { get; }
    public string   TimeText => Time.ToString("HH:mm:ss.fff");

    public string ForegroundColor => Level switch
    {
        MsgLevel.Send    => "#60A5FA",
        MsgLevel.Receive => "#4ADE80",
        MsgLevel.Alarm   => "#F87171",
        MsgLevel.Error   => "#FB923C",
        _                => "#E2E8F0",
    };

    public LogEntry(DateTime time, string message, MsgLevel level)
    {
        Time    = time;
        Message = message;
        Level   = level;
    }
}
