using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using R3;
using System.Collections.ObjectModel;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 出金（払出）コンポーネントを制御する ViewModel。
/// </summary>
public class DispenseViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    private readonly DispenseController _controller;
    private readonly ConfigurationProvider _configProvider;
    private readonly ILogger<DispenseViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

    // Properties
    public BindableReactiveProperty<decimal> TotalAmount { get; }
    public BindableReactiveProperty<string> DispenseAmountInput { get; }
    public ReactiveCommand DispenseCommand { get; }

    // Bulk Dispense
    public ObservableCollection<BulkInsertItemViewModel> BulkDispenseItems { get; } = [];
    public ReactiveCommand ShowBulkDispenseCommand { get; }
    public ReactiveCommand DispenseBulkCommand { get; }
    public ReactiveCommand CancelBulkDispenseCommand { get; }

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

        // Phase 18: UI Alignment
        // Expose Status for UI State switching
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
            .ToBindableReactiveProperty(Status.Value.ToString())
            .AddTo(_disposables);

        TotalAmount = new BindableReactiveProperty<decimal>(_inventory.CalculateTotal(_configProvider.Config.CurrencyCode)).AddTo(_disposables);
        _inventory.Changed
            .Subscribe(key =>
            {
                var total = _inventory.CalculateTotal(_configProvider.Config.CurrencyCode);
                Console.WriteLine($"Inventory Changed: {key}. New Total ({_configProvider.Config.CurrencyCode}): {total}");
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

        // Bulk Dispense
        ShowBulkDispenseCommand = isInDepositMode
            .CombineLatest(isJammed, (inDeposit, jammed) => !inDeposit && !jammed)
            .CombineLatest(IsBusy, (can, busy) => can && !busy)
            .ToReactiveCommand()
            .AddTo(_disposables);

        ShowBulkDispenseCommand.Subscribe(_ =>
        {
            PrepareBulkDispenseItems(getDenominations());
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                var window = new BulkDispenseWindow(this) { Owner = mainWindow };
                window.Show();
            }
        });

        DispenseBulkCommand = new ReactiveCommand().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(_ => ExecuteBulkDispense());

        CancelBulkDispenseCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkDispenseCommand.Subscribe(_ => { });

        ClearErrorCommand = Status
            .Select(s => s == CashDispenseStatus.Error)
            .ToReactiveCommand()
            .AddTo(_disposables);

        ClearErrorCommand.Subscribe(_ => _controller.ClearError());

        DispensingAmount = new BindableReactiveProperty<decimal>(0m).AddTo(_disposables);
    }

    // Phase 18: Properties
    public BindableReactiveProperty<CashDispenseStatus> Status { get; }
    public BindableReactiveProperty<string> StatusName { get; }
    public BindableReactiveProperty<bool> IsBusy { get; }
    public BindableReactiveProperty<decimal> DispensingAmount { get; } // For display
    public ReactiveCommand ClearErrorCommand { get; }

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
            // Swallowed: error is logged and shown to user, but we don't want to crash the UI thread
        }
    }

    private void PrepareBulkDispenseItems(IEnumerable<DenominationViewModel> denominations)
    {
        BulkDispenseItems.Clear();
        foreach (var den in denominations)
        {
            BulkDispenseItems.Add(new BulkInsertItemViewModel(den.Key, den.Name));
        }
    }

    private void ExecuteBulkDispense()
    {
        var counts = BulkDispenseItems
            .Where(x => x.Quantity.Value > 0)
            .ToDictionary(x => x.Key, x => x.Quantity.Value);

        if (counts.Count > 0)
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
                // Swallowed: error is logged and shown to user, but we don't want to crash the UI thread
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
