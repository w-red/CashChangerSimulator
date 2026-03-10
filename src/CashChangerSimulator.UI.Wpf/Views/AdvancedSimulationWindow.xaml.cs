using CashChangerSimulator.UI.Wpf.ViewModels;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// AdvancedSimulationWindow.xaml の相互作用ロジック
/// </summary>
internal partial class AdvancedSimulationWindow : Window
{
    public AdvancedSimulationWindow(AdvancedSimulationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
