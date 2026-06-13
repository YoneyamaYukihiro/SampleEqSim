using CommunityToolkit.Mvvm.ComponentModel;

namespace SampleEqSim.Host.ViewModels;

/// <summary>
/// シーケンス監視の1ステップ表示。状態を LED 色で示す。
/// LedGray=未到達 / LedYellow=次に期待 / LedGreen=受信済 / LedRed=スキップ・逸脱。
/// </summary>
public partial class SequenceStepViewModel : ObservableObject
{
    public string Sf   { get; }
    public string Desc { get; }
    public string Title => $"{Sf}  {Desc}";

    public SequenceStepViewModel(string sf, string desc)
    {
        Sf = sf;
        Desc = desc;
    }

    [ObservableProperty] private string _statusBrush = "LedGray";
    [ObservableProperty] private string _detail = "未到達";
}
