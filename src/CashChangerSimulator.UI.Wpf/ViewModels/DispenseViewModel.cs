using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>出金（払出）コンポーネントを制御する ViewModel。</summary>
public class DispenseViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    private readonly DispenseController _controller;
    private readonly ConfigurationProvider _configProvider;
    private readonly ILogger<DispenseViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

    // Properties
    /// <summary>現在の在庫合計金額。</summary>
    public BindableReactiveProperty<decimal> TotalAmount { get; }
    /// <summary>出金金額の入力値。</summary>
    public BindableReactiveProperty<string> DispenseAmountInput { get; }
    /// <summary>出金を実行するコマンド。</summary>
    public ReactiveCommand DispenseCommand { get; }

    // Bulk Dispense
    /// <summary>一括出金画面を表示するコマンド（View側で購読）。</summary>
    public ReactiveCommand ShowBulkDispenseCommand { get; }
    /// <summary>一括出金を実行するコマンド。</summary>
    public ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>> DispenseBulkCommand { get; }

    // Phase 18: State Properties
    /// <summary>出金ステータス。</summary>
    public BindableReactiveProperty<CashDispenseStatus> Status { get; }
    /// <summary>出金ステータスの表示名。</summary>
    public BindableReactiveProperty<string> StatusName { get; }
    /// <summary>出金処理中かどうか。</summary>
    public BindableReactiveProperty<bool> IsBusy { get; }
    /// <summary>現在出金中の合計金額。</summary>
    public BindableReactiveProperty<decimal> DispensingAmount { get; }
    /// <summary>エラー状態をクリアするコマンド。</summary>
    public ReactiveCommand ClearErrorCommand { get; }

    /// <summary>DispenseViewModel の新しいインスタンスを初期化します。</summary>
    public DispenseViewModel(
        Inventory inventory,
        CashChangerManager manager,
        DispenseController controller,
        ConfigurationProvider configProvider,
        Observable<bool> isInDepositMode,
        Observable<bool> isJammed,
        Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        _inventory = inventory;
        _manager = manager;
        _controller = controller;
        _configProvider = configProvider;
        _logger = LogProvider.CreateLogger<DispenseViewModel>();

        // State Mapping
        Status = _controller.Changed
            .Select(_ => _controller.Status)
            .ToBindableReactiveProperty(_controller.Status)
            .AddTo(_disposables);

        IsBusy = Status
            .Select(s => s == CashDispenseStatus.Busy)
            .ToBindableReactiveProperty(false)
            .AddTo(_disposables);

        StatusName = Status
            .Select(s => s.ToString())
            .ToBindableReactiveProperty("Idle")
            .AddTo(_disposables);

        DispensingAmount = new BindableReactiveProperty<decimal>(0m).AddTo(_disposables);

        TotalAmount = new BindableReactiveProperty<decimal>(_inventory.CalculateTotal(_configProvider.Config.CurrencyCode)).AddTo(_disposables);
        _inventory.Changed
            .Subscribe(_ =>
            {
                var total = _inventory.CalculateTotal(_configProvider.Config.CurrencyCode);
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => TotalAmount.Value = total);
                }
                else
                {
                    TotalAmount.Value = total;
                }
            })
            .AddTo(_disposables);

        DispenseAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text)
                    ? null
                    : !decimal.TryParse(text, out var val)
                    ? new Exception("Enter a valid number")
                    : val <= 0
                    ? new Exception("Amount must be positive")
                    : val > TotalAmount.Value
                    ? new Exception("Insufficient funds")
                    : null
            )
            .AddTo(_disposables);

        DispenseCommand = DispenseAmountInput
            .Select(_ => !DispenseAmountInput.HasErrors && !string.IsNullOrWhiteSpace(DispenseAmountInput.Value))
            .CombineLatest(isInDepositMode, (canDispenseInput, mode) => canDispenseInput && !mode)
            .CombineLatest(IsBusy, (can, busy) => can && !busy)
            .ToReactiveCommand()
            .AddTo(_disposables);

        DispenseCommand.Subscribe(_ =>
        {
            if (decimal.TryParse(DispenseAmountInput.Value, out var amount))
            {
                DispenseCash(amount);
                DispenseAmountInput.Value = "";
            }
        });

        ShowBulkDispenseCommand = isInDepositMode
            .CombineLatest(isJammed, (inDeposit, jammed) => !inDeposit && !jammed)
            .CombineLatest(IsBusy, (can, busy) => can && !busy)
            .ToReactiveCommand()
            .AddTo(_disposables);

        DispenseBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(counts =>
        {
            if (counts != null && counts.Count > 0)
            {
                ExecuteBulkDispense(counts);
            }
        });

        ClearErrorCommand = Status
            .Select(s => s == CashDispenseStatus.Error)
            .ToReactiveCommand()
            .AddTo(_disposables);

        ClearErrorCommand.Subscribe(_ => _controller.ClearError());
    }

    private void DispenseCash(decimal amount)
    {
        try
        {
            DispensingAmount.Value = amount;
            _ = _controller.DispenseChangeAsync(amount, true, (code, ext) => { }, _configProvider.Config.CurrencyCode);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense {amount}.");
            System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void ExecuteBulkDispense(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        try
        {
            var total = counts.Sum(x => x.Key.Value * x.Value);
            DispensingAmount.Value = total;
            _ = _controller.DispenseCashAsync(counts, true, (code, ext) => { });
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense cash (bulk).");
            System.Windows.MessageBox.Show(ex.Message, "Dispense Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
