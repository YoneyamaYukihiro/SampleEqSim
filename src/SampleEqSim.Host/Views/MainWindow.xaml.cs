using System.Windows;
using SampleEqSim.Host.ViewModels;

namespace SampleEqSim.Host.Views;

public partial class MainWindow : Window
{
    public MainWindow(HostViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // 装置とホストを並べて使えるよう、起動時に画面の右半分 (縦は全高) に配置する。
        // (タスクバーを除いた作業領域 WorkArea 基準。DIP 単位で WPF と一致)
        var wa = SystemParameters.WorkArea;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left   = wa.Left + wa.Width / 2;
        Top    = wa.Top;
        Width  = wa.Width / 2;
        Height = wa.Height;
    }
}
