using CashChangerSimulator.UI.Wpf.ViewModels;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// AdvancedSimulationWindow.xaml の相互作用ロジック
/// </summary>
public partial class AdvancedSimulationWindow : Window
{
    public AdvancedSimulationWindow(AdvancedSimulationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
