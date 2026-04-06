using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SampleEqSim.Core.Gem;

namespace SampleEqSim.Equipment.ViewModels;

public partial class EquipmentViewModel : ObservableObject
{
    private readonly GemEquipmentModel _model;
    private readonly DispatcherTimer _uiUpdateTimer;

    // ── 状態表示 ──────────────────────────────────────────────────
    [ObservableProperty] private string _communicationStateText = "DISABLED";
    [ObservableProperty] private string _controlStateText = "EQUIPMENT_OFFLINE";
    [ObservableProperty] private string _processingStateText = "INIT";
    [ObservableProperty] private string _communicationStateBrush = "LedGray";
    [ObservableProperty] private string _controlStateBrush = "LedGray";
    [ObservableProperty] private string _processingStateBrush = "LedGray";

    // ── 装置値 ────────────────────────────────────────────────────
    [ObservableProperty] private float _temperature = 25.0f;
    [ObservableProperty] private float _pressure = 101.3f;
    [ObservableProperty] private string _lotId = "";
    [ObservableProperty] private string _recipeId = "";

    // ── アラーム ──────────────────────────────────────────────────
    public ObservableCollection<AlarmViewModel> AlarmList { get; } = new();

    // ── ログ ──────────────────────────────────────────────────────
    public ObservableCollection<string> MessageLog { get; } = new();
    private const int MaxLogLines = 1000;

    // ── 時刻表示 ──────────────────────────────────────────────────
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    public EquipmentViewModel(GemEquipmentModel model)
    {
        _model = model;

        // アラームビューモデルを初期化
        foreach (var alarm in model.Alarms.Values)
            AlarmList.Add(new AlarmViewModel(alarm));

        // モデルイベント購読
        model.CommunicationStateChanged += (_, s) => App.Current.Dispatcher.Invoke(() => UpdateCommunicationState(s));
        model.ControlStateChanged += (_, s) => App.Current.Dispatcher.Invoke(() => UpdateControlState(s));
        model.ProcessingStateChanged += (_, s) => App.Current.Dispatcher.Invoke(() => UpdateProcessingState(s));
        model.MessageLogged += (_, msg) => App.Current.Dispatcher.Invoke(() => AddLog(msg));
        model.AlarmStateChanged += (_, e) => App.Current.Dispatcher.Invoke(() => UpdateAlarm(e.AlarmId, e.IsSet));

        // UI更新タイマー (1秒ごと)
        _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uiUpdateTimer.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            // 温度・圧力の軽微な揺らぎをシミュレート
            if (model.ProcessingState == ProcessingState.Executing)
            {
                Temperature += (float)(new Random().NextDouble() - 0.5) * 0.5f;
                Pressure += (float)(new Random().NextDouble() - 0.5) * 0.2f;
                model.SetTemperature(Temperature);
                model.SetPressure(Pressure);
            }
        };
        _uiUpdateTimer.Start();
    }

    // ─────────────────────────────────────────────────────────────
    // 状態更新
    // ─────────────────────────────────────────────────────────────
    private void UpdateCommunicationState(CommunicationState state)
    {
        CommunicationStateText = state.ToString().ToUpper();
        CommunicationStateBrush = state switch
        {
            CommunicationState.Communicating => "LedGreen",
            CommunicationState.WaitCrFromHost or CommunicationState.WaitCr => "LedYellow",
            CommunicationState.WaitDelay => "LedYellow",
            _ => "LedGray",
        };
    }

    private void UpdateControlState(ControlState state)
    {
        ControlStateText = state.ToString().ToUpper();
        ControlStateBrush = state switch
        {
            ControlState.OnlineRemote => "LedGreen",
            ControlState.OnlineLocal => "LedBlue",
            ControlState.AttemptOnline => "LedYellow",
            ControlState.HostOffline => "LedRed",
            _ => "LedGray",
        };
    }

    private void UpdateProcessingState(ProcessingState state)
    {
        ProcessingStateText = state.ToString().ToUpper();
        ProcessingStateBrush = state switch
        {
            ProcessingState.Executing => "LedGreen",
            ProcessingState.Ready => "LedBlue",
            ProcessingState.Setup => "LedYellow",
            ProcessingState.Pause => "LedYellow",
            ProcessingState.Idle => "LedGray",
            _ => "LedGray",
        };
    }

    private void UpdateAlarm(uint alarmId, bool isSet)
    {
        var vm = AlarmList.FirstOrDefault(a => a.AlarmId == alarmId);
        if (vm != null) vm.IsSet = isSet;
    }

    private void AddLog(string message)
    {
        MessageLog.Insert(0, message);
        while (MessageLog.Count > MaxLogLines)
            MessageLog.RemoveAt(MessageLog.Count - 1);
    }

    // ─────────────────────────────────────────────────────────────
    // コマンド: 処理状態操作
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void SetIdle() => _model.SetProcessingState(ProcessingState.Idle);

    [RelayCommand]
    private void SetSetup() => _model.SetProcessingState(ProcessingState.Setup);

    [RelayCommand]
    private void SetReady() => _model.SetProcessingState(ProcessingState.Ready);

    [RelayCommand]
    private void SetExecuting() => _model.SetProcessingState(ProcessingState.Executing);

    [RelayCommand]
    private void SetPause() => _model.SetProcessingState(ProcessingState.Pause);

    // ─────────────────────────────────────────────────────────────
    // コマンド: アラーム操作
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SetAlarm(object? parameter)
    {
        if (uint.TryParse(parameter?.ToString(), out var alarmId))
            await _model.SetAlarmAsync(alarmId, true);
    }

    [RelayCommand]
    private async Task ClearAlarm(object? parameter)
    {
        if (uint.TryParse(parameter?.ToString(), out var alarmId))
            await _model.SetAlarmAsync(alarmId, false);
    }

    [RelayCommand]
    private async Task ClearAllAlarms()
    {
        foreach (var alarm in _model.Alarms.Values)
            await _model.SetAlarmAsync(alarm.AlarmId, false);
    }

    // ─────────────────────────────────────────────────────────────
    // コマンド: 装置値更新
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private void UpdateValues()
    {
        _model.SetTemperature(Temperature);
        _model.SetPressure(Pressure);
        _model.SetLotId(LotId);
        _model.SetRecipeId(RecipeId);
    }

    // ─────────────────────────────────────────────────────────────
    // コマンド: イベント送信
    // ─────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SendLotStarted()
    {
        _model.SetLotId(LotId);
        await _model.SendCollectionEventAsync(103); // LotStarted
    }

    [RelayCommand]
    private async Task SendLotCompleted()
    {
        await _model.SendCollectionEventAsync(104); // LotCompleted
    }

    [RelayCommand]
    private void ClearLog() => MessageLog.Clear();
}

// ─────────────────────────────────────────────────────────────
// アラームビューモデル
// ─────────────────────────────────────────────────────────────
public partial class AlarmViewModel : ObservableObject
{
    public uint AlarmId { get; }
    public string AlarmCode { get; }
    public string AlarmText { get; }
    public AlarmCategory Category { get; }
    [ObservableProperty] private bool _isSet;
    [ObservableProperty] private bool _isEnabled;

    public AlarmViewModel(AlarmDefinition alarm)
    {
        AlarmId = alarm.AlarmId;
        AlarmCode = alarm.AlarmCode;
        AlarmText = alarm.AlarmText;
        Category = alarm.Category;
        IsSet = alarm.IsSet;
        IsEnabled = alarm.IsEnabled;
    }
}
