using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>
/// 一括投入（Bulk Insert）を行うためのウィンドウ。
/// </summary>
public partial class BulkInsertWindow : Window
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>
    /// BulkInsertWindow の新しいインスタンスを初期化する。
    /// </summary>
    /// <param name="viewModel">入金ビューモデル。</param>
    public BulkInsertWindow(DepositViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.InsertBulkCommand.Subscribe(_ => Close()).AddTo(_disposables);
        viewModel.CancelBulkInsertCommand.Subscribe(_ => Close()).AddTo(_disposables);

        Closed += (s, e) => _disposables.Dispose();
    }

}
