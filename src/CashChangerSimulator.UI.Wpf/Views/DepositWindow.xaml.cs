using CashChangerSimulator.Core;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.Extensions.Logging;
using ZLogger;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>入金操作を行うための独立ウィンドウ。</summary>
internal partial class DepositWindow : Window
{
    private readonly ILogger<DepositWindow> _logger = LogProvider.CreateLogger<DepositWindow>();
    private readonly CompositeDisposable _disposables = [];

    /// <summary>DepositWindow の新しいインスタンスを初期化する。</summary>
    /// <param name="viewModel">入金ビューモデル。</param>
    /// <param name="getDenominations">金種リスト取得関数。</param>
    /// <param name="factory">ViewModel 生成ファクトリ。</param>
    public DepositWindow(DepositViewModel viewModel, Func<IEnumerable<DenominationViewModel>> getDenominations, IViewModelFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        InitializeComponent();
        DataContext = viewModel;

        viewModel.ShowBulkInsertCommand.Subscribe(_ =>
        {
            _logger.ZLogInformation($"Bulk insert button clicked. Generating item viewmodels.");
            var items = getDenominations().Select(d => factory.CreateBulkAmountInputItemViewModel(d.Key, d.Name)).ToList();
            var bulkVm = factory.CreateBulkAmountInputViewModel(
                items,
                viewModel.SimulateOverlapCommand,
                viewModel.SimulateJamCommand,
                viewModel.SimulateDeviceErrorCommand,
                viewModel.ResetErrorCommand,
                viewModel.IsJammed,
                viewModel.IsOverlapped,
                viewModel.IsDeviceError.ToReadOnlyReactiveProperty());

            var dialog = new BulkAmountInputWindow(
                FindResource("BulkDeposit").ToString()!,
                FindResource("BulkDepositSubtitle").ToString(),
                FindResource("Deposit").ToString())
            {
                Owner = this,
                DataContext = bulkVm
            };

            _logger.ZLogInformation($"Showing BulkAmountInputWindow as dialog.");
            _logger.ZLogInformation($"Showing BulkAmountInputWindow as dialog.");
            if (dialog.ShowDialog() == true)
            {
                _logger.ZLogInformation($"BulkAmountInputWindow returned true.");
                _logger.ZLogInformation($"BulkAmountInputWindow returned true.");
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
