using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>入金操作を行うための独立ウィンドウ。</summary>
internal partial class DepositWindow : Window
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>DepositWindow の新しいインスタンスを初期化する。</summary>
    /// <param name="viewModel">入金ビューモデル。</param>
    /// <param name="getDenominations">金種リスト取得関数。</param>
    public DepositWindow(DepositViewModel viewModel, Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ShowBulkInsertCommand.Subscribe(_ =>
        {
            var items = getDenominations().Select(d => new BulkAmountInputItemViewModel(d.Key, d.Name)).ToList();
            var bulkVm = new BulkAmountInputViewModel(
                items,
                viewModel.SimulateOverlapCommand,
                viewModel.SimulateJamCommand,
                viewModel.ResetErrorCommand,
                viewModel.IsJammed,
                viewModel.IsOverlapped);

            var dialog = new BulkAmountInputWindow(
                FindResource("BulkDeposit").ToString()!,
                FindResource("BulkDepositSubtitle").ToString(),
                FindResource("Deposit").ToString())
            {
                Owner = this,
                DataContext = bulkVm
            };

            if (dialog.ShowDialog() == true)
            {
                var counts = items.Where(x => x.Quantity.Value > 0)
                                  .ToDictionary(x => x.Key, x => x.Quantity.Value);
                viewModel.InsertBulkCommand.Execute(counts);
            }
        }).AddTo(_disposables);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }
}
