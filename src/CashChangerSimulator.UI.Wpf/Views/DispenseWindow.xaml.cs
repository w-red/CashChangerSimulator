using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>出金操作を行うための独立ウィンドウ。</summary>
public partial class DispenseWindow : Window
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>DispenseWindow の新しいインスタンスを初期化する。</summary>
    /// <param name="viewModel">出金ビューモデル。</param>
    /// <param name="getDenominations">金種リスト取得関数。</param>
    public DispenseWindow(DispenseViewModel viewModel, Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ShowBulkDispenseCommand.Subscribe(_ =>
        {
            var items = getDenominations().Select(d => new BulkAmountInputItemViewModel(d.Key, d.Name)).ToList();
            var bulkVm = new BulkAmountInputViewModel(
                items,
                viewModel.SimulateOverlapCommand,
                viewModel.SimulateJamCommand,
                viewModel.ResetErrorCommand,
                viewModel.IsJammed,
                viewModel.IsOverlapped);

            var dialog = new BulkAmountInputWindow("BULK DISPENSE", "Please enter the quantity to dispense.", "DISPENSE")
            { 
                Owner = this,
                DataContext = bulkVm 
            };

            if (dialog.ShowDialog() == true)
            {
                var counts = items.Where(x => x.Quantity.Value > 0)
                                  .ToDictionary(x => x.Key, x => x.Quantity.Value);
                viewModel.DispenseBulkCommand.Execute(counts);
            }
        }).AddTo(_disposables);
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        base.OnClosed(e);
    }
}
