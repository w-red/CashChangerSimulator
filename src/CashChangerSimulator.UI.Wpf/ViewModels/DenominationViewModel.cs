using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>各金種の表示と操作を管理する ViewModel。</summary>
public class DenominationViewModel
{
    private readonly Inventory _inventory;
    public DenominationKey Key { get; }
    public string Name { get; }
    private readonly BindableReactiveProperty<int> _count;
    public BindableReactiveProperty<int> Count { get; }
    public BindableReactiveProperty<CashStatus> Status { get; }
    public BindableReactiveProperty<bool> IsAcceptingCash { get; }

    public DenominationViewModel(Inventory inventory, DenominationKey key, Services.CurrencyMetadataProvider metadataProvider, DepositController depositController, CashStatusMonitor monitor, string? displayName = null)
    {
        _inventory = inventory;
        Key = key;
        Name = !string.IsNullOrEmpty(displayName) ? displayName : metadataProvider.GetDenominationName(key);
        Status = monitor.Status.ToBindableReactiveProperty();
        _count = new BindableReactiveProperty<int>(_inventory.GetCount(key));
        Count = _count;
        _inventory.Changed
            .Where(k => k.Value == key.Value && k.Type == key.Type && k.CurrencyCode == key.CurrencyCode)
            .Subscribe(_ =>
            {
                var newCount = _inventory.GetCount(key);
                System.Diagnostics.Debug.WriteLine($"[DenominationViewModel] Updated {key}: {newCount}");
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _count.Value = newCount);
                }
                else
                {
                    _count.Value = newCount;
                }
            });

        IsAcceptingCash = depositController.Changed
            .Select(_ => depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused)
            .ToBindableReactiveProperty(depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused);
    }
}
