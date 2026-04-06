using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Secs4Net;
using static Secs4Net.Item;

namespace SampleEqSim.Host.ViewModels;

public partial class HostViewModel : ObservableObject
{
    private readonly ISecsGem _secsGem;

    // 笏笏 謗･邯夂憾諷・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ObservableProperty] private string _connectionStateText = "DISCONNECTED";
    [ObservableProperty] private string _connectionLedBrush = "LedGray";
    [ObservableProperty] private bool _isConnected = false;

    // 笏笏 陬・ｽｮ諠・ｱ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ObservableProperty] private string _equipmentModel = "-";
    [ObservableProperty] private string _equipmentSoftRev = "-";
    [ObservableProperty] private string _equipmentDateTime = "-";
    [ObservableProperty] private string _equipmentControlState = "-";
    [ObservableProperty] private string _equipmentProcState = "-";

    // 笏笏 繧ｳ繝槭Φ繝牙・蜉・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ObservableProperty] private string _hostCommandText = "START";

    // 笏笏 SVID繝ｪ繧ｯ繧ｨ繧ｹ繝・笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ObservableProperty] private string _requestSvIds = "101,102,103";
    [ObservableProperty] private string _svDataResult = "";

    // 笏笏 繝ｭ繧ｰ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public ObservableCollection<LogEntry> MessageLog { get; } = new();
    private const int MaxLogLines = 2000;

    // 笏笏 蜿嶺ｿ｡繧､繝吶Φ繝医Ο繧ｰ 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    public ObservableCollection<string> EventLog { get; } = new();

    // 笏笏 譎ょ綾 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    private readonly DispatcherTimer _clockTimer;

    public HostViewModel(ISecsGem secsGem)
    {
        _secsGem = secsGem;

        

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _clockTimer.Start();
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 謗･邯夂憾諷句､牙喧
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private void OnConnectionChanged(object? sender, ConnectionState state)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectionStateText = state.ToString().ToUpper();
            IsConnected = state == ConnectionState.Connected;
            ConnectionLedBrush = state switch
            {
                ConnectionState.Connected => "LedGreen",
                ConnectionState.Retry => "LedYellow",
                _ => "LedGray",
            };
            AddLog($"[HSMS] 謗･邯夂憾諷句､牙喧: {state}", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 蜿嶺ｿ｡繝｡繝・そ繝ｼ繧ｸ蜃ｦ逅・(陬・ｽｮ縺九ｉ縺ｮ繝励Λ繧､繝槭Μ繝｡繝・そ繝ｼ繧ｸ)
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private async void OnPrimaryMessageReceived(object? sender, PrimaryMessageWrapper e)
    {
        var msg = e.PrimaryMessage;
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
            AddLog($"RCV << S{msg.S}F{msg.F} ", LogLevel.Receive));

        SecsMessage? reply = (msg.S, msg.F) switch
        {
            // S5F1: Alarm Report
            (5, 1) => HandleS5F1(msg),
            // S6F11: Event Report
            (6, 11) => HandleS6F11(msg),
            // S10F1: Terminal Request
            (10, 1) => HandleS10F1(msg),
            _ => null
        };

        if (msg.ReplyExpected && reply != null)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                AddLog($"SND >> S{reply.S}F{reply.F} ", LogLevel.Send));
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
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var ev = $"[ALARM {(isSet ? "SET" : "CLR")}] ALID={alid} {alTx}";
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {ev}");
                AddLog(ev, isSet ? LogLevel.Alarm : LogLevel.System);
            });
        }
        return new SecsMessage(5, 2) { SecsItem = B(0) };
    }

    private SecsMessage HandleS6F11(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var ceid = msg.SecsItem[1].FirstValue<uint>();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [EVENT] CEID={ceid}");
                while (EventLog.Count > 200) EventLog.RemoveAt(EventLog.Count - 1);
            });
        }
        return new SecsMessage(6, 12) { SecsItem = B(0) };
    }

    private SecsMessage HandleS10F1(SecsMessage msg)
    {
        if (msg.SecsItem?.Count >= 2)
        {
            var text = msg.SecsItem[1].GetString();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] [TERMINAL] {text}"));
        }
        return new SecsMessage(10, 2) { SecsItem = B(0) };
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S1F1 Are You There
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendS1F1()
    {
        await SendAndLog(new SecsMessage(1, 1), reply =>
        {
            if (reply?.SecsItem?.Count >= 2)
            {
                EquipmentModel = reply.SecsItem[0].GetString();
                EquipmentSoftRev = reply.SecsItem[1].GetString();
            }
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S1F13 Establish Communications
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendS1F13()
    {
        await SendAndLog(
            new SecsMessage(1, 13) { SecsItem = L(A("HOST"), A("1.0")) },
            reply =>
            {
                if (reply?.SecsItem?.Count >= 2)
                {
                    var commack = reply.SecsItem[0].FirstValue<byte>();
                    AddLog($"  COMMACK={commack} ({(commack == 0 ? "Accepted" : "Rejected")})", LogLevel.System);
                }
            });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S1F15 Request OFF-LINE
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendS1F15()
    {
        await SendAndLog(new SecsMessage(1, 15), reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  OFLACK={ack}", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S1F17 Request ON-LINE
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendS1F17()
    {
        await SendAndLog(new SecsMessage(1, 17), reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  ONLACK={ack} ({(ack == 0 ? "OK" : ack == 2 ? "Already Online" : "Refused")})", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S2F17 Date and Time Request
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendS2F17()
    {
        await SendAndLog(new SecsMessage(2, 17), reply =>
        {
            EquipmentDateTime = reply?.SecsItem?.GetString() ?? "-";
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S1F3 Status Variables Request
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task RequestStatusVariables()
    {
        var ids = RequestSvIds.Split(',')
            .Select(s => s.Trim())
            .Where(s => uint.TryParse(s, out _))
            .Select(uint.Parse)
            .ToList();

        var svItems = ids.Select(id => (Item)U4(id)).ToList();
        var msg = new SecsMessage(1, 3) { SecsItem = L(svItems) };

        await SendAndLog(msg, reply =>
        {
            if (reply?.SecsItem != null)
            {
                var results = reply.SecsItem.Items.Select((item, i) =>
                    $"SV[{(i < ids.Count ? ids[i].ToString() : "?")}] = {item.ToString()}").ToList();
                SvDataResult = string.Join("\n", results);
            }
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S5F5 List Alarms
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task ListAlarms()
    {
        await SendAndLog(new SecsMessage(5, 5) { SecsItem = L() }, reply =>
        {
            if (reply?.SecsItem != null)
            {
                foreach (var alarmItem in reply.SecsItem.Items)
                {
                    if (alarmItem.Count >= 3)
                    {
                        var alid = alarmItem[1].FirstValue<uint>();
                        var text = alarmItem[2].GetString();
                        AddLog($"  ALARM ALID={alid}: {text}", LogLevel.System);
                    }
                }
            }
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S2F41 Host Command
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task SendHostCommand()
    {
        if (string.IsNullOrWhiteSpace(HostCommandText)) return;
        var msg = new SecsMessage(2, 41)
        {
            SecsItem = L(A(HostCommandText.ToUpper()), L())
        };
        await SendAndLog(msg, reply =>
        {
            if (reply?.SecsItem?.Count >= 1)
            {
                var hcack = reply.SecsItem[0].FirstValue<byte>();
                AddLog($"  HCACK={hcack} ({(hcack == 0 ? "ACK" : "NACK")})", LogLevel.System);
            }
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S2F33 Define Report
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task DefineReport()
    {
        // RPTID=1: SV 101 (Temperature), 102 (Pressure), 103 (LotId)
        var msg = new SecsMessage(2, 33)
        {
            SecsItem = L(
                U4(1),     // DATAID
                L(
                    L(U4(1), L(U4(101u), U4(102u), U4(103u)))))  // RPTID=1, VIDs
        };
        await SendAndLog(msg, reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  DRACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S2F35 Link Event Report
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task LinkEventReport()
    {
        // CEID=101 (ProcessStarted) 竊・RPTID=1
        var msg = new SecsMessage(2, 35)
        {
            SecsItem = L(
                U4(1),   // DATAID
                L(
                    L(U4(101u), L(U4(1u))),   // CEID=101 竊・RPTID=1
                    L(U4(102u), L(U4(1u)))))  // CEID=102 竊・RPTID=1
        };
        await SendAndLog(msg, reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  LRACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繧ｳ繝槭Φ繝・ S2F37 Enable/Disable Events (蜈ｨ譛牙柑)
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    [RelayCommand]
    private async Task EnableAllEvents()
    {
        var msg = new SecsMessage(2, 37)
        {
            SecsItem = L(Boolean(true), L()) // 蜈ｨCEID譛牙柑
        };
        await SendAndLog(msg, reply =>
        {
            var ack = reply?.SecsItem?.FirstValue<byte>() ?? 0xFF;
            AddLog($"  ERACK={ack} ({(ack == 0 ? "ACK" : "NACK")})", LogLevel.System);
        });
    }

    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    // 繝倥Ν繝代・: 繝｡繝・そ繝ｼ繧ｸ騾∽ｿ｡ + 繝ｭ繧ｰ
    // 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
    private async Task SendAndLog(SecsMessage msg, Action<SecsMessage?>? onReply = null)
    {
        if (!IsConnected)
        {
            AddLog("[ERR] Not connected", LogLevel.Error);
            return;
        }
        try
        {
            AddLog($"SND >> S{msg.S}F{msg.F} ", LogLevel.Send);
            var reply = await _secsGem.SendAsync(msg);
            if (reply != null)
                AddLog($"RCV << S{reply.S}F{reply.F} ", LogLevel.Receive);
            onReply?.Invoke(reply);
        }
        catch (SecsException ex)
        {
            AddLog($"[ERR] S{msg.S}F{msg.F}: {ex.Message}", LogLevel.Error);
        }
        catch (Exception ex)
        {
            AddLog($"[ERR] {ex.Message}", LogLevel.Error);
        }
    }

    private void AddLog(string message, LogLevel level = LogLevel.System)
    {
        var entry = new LogEntry(DateTime.Now, message, level);
        MessageLog.Insert(0, entry);
        while (MessageLog.Count > MaxLogLines)
            MessageLog.RemoveAt(MessageLog.Count - 1);
    }

    [RelayCommand]
    private void ClearLog() => MessageLog.Clear();

    [RelayCommand]
    private void ClearEventLog() => EventLog.Clear();
}

// 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
// 繝ｭ繧ｰ繧ｨ繝ｳ繝医Μ
// 笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏笏
public enum LogLevel { System, Send, Receive, Alarm, Error }

public class LogEntry
{
    public DateTime Time { get; }
    public string Message { get; }
    public LogLevel Level { get; }
    public string TimeText => Time.ToString("HH:mm:ss.fff");

    public string ForegroundColor => Level switch
    {
        LogLevel.Send    => "#60A5FA",  // 髱・
        LogLevel.Receive => "#4ADE80",  // 邱・
        LogLevel.Alarm   => "#F87171",  // 襍､
        LogLevel.Error   => "#FB923C",  // 繧ｪ繝ｬ繝ｳ繧ｸ
        _                => "#E2E8F0",  // 逋ｽ
    };

    public LogEntry(DateTime time, string message, LogLevel level)
    {
        Time = time;
        Message = message;
        Level = level;
    }

    public override string ToString() => $"[{TimeText}] {Message}";
}


