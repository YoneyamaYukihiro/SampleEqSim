using System.Windows;
using SampleEqSim.Equipment.ViewModels;

namespace SampleEqSim.Equipment.Views;

public partial class MainWindow : Window
{
    public MainWindow(EquipmentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 装置とホストを並べて使えるよう、起動時に画面の左半分 (縦は全高) に配置する。
        // (タスクバーを除いた作業領域 WorkArea 基準。DIP 単位で WPF と一致)
        var wa = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left   = wa.Left;
        Top    = wa.Top;
        Width  = wa.Width / 2;
        Height = wa.Height;
    }
}
