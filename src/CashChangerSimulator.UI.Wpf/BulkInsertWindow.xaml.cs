using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>
/// 一括投入（Bulk Insert）を行うためのウィンドウ。
/// </summary>
public partial class BulkInsertWindow : Window
{
    /// <summary>
    /// BulkInsertWindow の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="viewModel">メインビューモデル。</param>
    public BulkInsertWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.InsertBulkCommand.Subscribe(_ => Close());
        viewModel.CancelBulkInsertCommand.Subscribe(_ => Close());
    }
}
