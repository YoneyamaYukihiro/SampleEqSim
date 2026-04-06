using System.Windows;
using SampleEqSim.Host.ViewModels;

namespace SampleEqSim.Host.Views;

public partial class MainWindow : Window
{
    public MainWindow(HostViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
