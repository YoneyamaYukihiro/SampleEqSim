using System.Windows;
using SampleEqSim.Equipment.ViewModels;

namespace SampleEqSim.Equipment.Views;

public partial class MainWindow : Window
{
    public MainWindow(EquipmentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
