using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>一括払出（Bulk Dispense）を行うためのウィンドウ。</summary>
public partial class BulkDispenseWindow : Window
{
    /// <summary>BulkDispenseWindow の新しいインスタンスを初期化する。</summary>
    /// <param name="viewModel">メインビューモデル。</param>
    public BulkDispenseWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.DispenseBulkCommand.Subscribe(_ => Close());
        viewModel.CancelBulkDispenseCommand.Subscribe(_ => Close());
    }
}
