using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.UI.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = DIContainer.Resolve<MainViewModel>();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}