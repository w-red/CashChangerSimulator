using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>一括払出（Bulk Dispense）を行うためのウィンドウ。</summary>
public partial class BulkDispenseWindow : Window
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>BulkDispenseWindow の新しいインスタンスを初期化する。</summary>
    /// <param name="viewModel">出金ビューモデル。</param>
    public BulkDispenseWindow(DispenseViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Close window when commands execute
        viewModel.DispenseBulkCommand.Subscribe(_ => Close()).AddTo(_disposables);
        viewModel.CancelBulkDispenseCommand.Subscribe(_ => Close()).AddTo(_disposables);

        Closed += (s, e) => _disposables.Dispose();
    }

}
