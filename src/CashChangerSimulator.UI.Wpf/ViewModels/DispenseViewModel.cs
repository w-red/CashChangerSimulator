using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using R3;
using System.Collections.ObjectModel;

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
            .CombineLatest(_controller.Changed.Select(_ => !_controller.IsBusy).Prepend(!_controller.IsBusy), (can, notBusy) => can && notBusy)
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
            .CombineLatest(_controller.Changed.Select(_ => !_controller.IsBusy).Prepend(!_controller.IsBusy), (can, notBusy) => can && notBusy)
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
    }

    private void DispenseCash(decimal amount)
    {
        try
        {
            _manager.Dispense(amount, _configProvider.Config.CurrencyCode);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
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
                _manager.Dispense(counts);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Dispense Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
