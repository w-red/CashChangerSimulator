using System.Windows;
using System.Windows.Controls;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.UI.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        this.DataContext = _viewModel;
    }

    private void AddCash_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int denomination)
        {
            _viewModel.AddCash(denomination, 1);
        }
    }

    private void RemoveCash_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int denomination)
        {
            // For now, removing cash is just dispensing 1 count
            _viewModel.AddCash(denomination, -1);
        }
    }

    private void Dispense_Click(object sender, RoutedEventArgs e)
    {
        if (decimal.TryParse(DispenseTargetBox.Text, out decimal amount))
        {
            _viewModel.DispenseCash(amount);
            DispenseTargetBox.Clear();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}