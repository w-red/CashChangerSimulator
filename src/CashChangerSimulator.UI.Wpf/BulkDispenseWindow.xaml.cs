using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;

namespace CashChangerSimulator.UI.Wpf;

public partial class BulkDispenseWindow : Window
{
    public BulkDispenseWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.DispenseBulkCommand.Subscribe(_ => Close());
        viewModel.CancelBulkDispenseCommand.Subscribe(_ => Close());
    }
}
