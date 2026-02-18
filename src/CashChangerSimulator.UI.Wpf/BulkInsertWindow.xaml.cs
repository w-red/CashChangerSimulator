using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;

namespace CashChangerSimulator.UI.Wpf;

public partial class BulkInsertWindow : Window
{
    public BulkInsertWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.InsertBulkCommand.Subscribe(_ => Close());
        viewModel.CancelBulkInsertCommand.Subscribe(_ => Close());
    }
}
