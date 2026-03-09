using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;
using System.Linq;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>個別の金種（1000円、100円等）の表示状態と操作を管理する ViewModel。</summary>
public class DenominationViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    public DenominationKey Key { get; }
    public string Name { get; }
    public BindableReactiveProperty<int> Count { get; }
    public BindableReactiveProperty<CashStatus> Status { get; }
    public BindableReactiveProperty<bool> IsAcceptingCash { get; }

    public BindableReactiveProperty<int> RecyclableCount { get; }
    public BindableReactiveProperty<int> CollectionCount { get; }
    public BindableReactiveProperty<int> RejectCount { get; }

    public bool IsRecyclable { get; }
    public bool IsDepositable { get; }
    public ReactiveCommand<Unit> ShowDetailCommand { get; }

    public DenominationViewModel(
        Inventory inventory,
        DenominationKey key,
        CurrencyMetadataProvider metadataProvider,
        DepositController depositController,
        CashStatusMonitor monitor,
        ConfigurationProvider configProvider)
    {
        Key = key;

        var cultureCode = configProvider.Config.System.CultureCode ?? "en-US";
        var isJapanese = cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
        var keyStr = (key.Type == CurrencyCashType.Bill ? "B" : "C") + key.Value.ToString();

        string? name = null;
        if (configProvider.Config.Inventory.TryGetValue(key.CurrencyCode, out var inventorySettings) &&
            inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
        {
            name = isJapanese ? (setting.DisplayNameJP ?? setting.DisplayName) : setting.DisplayName;
            IsRecyclable = setting.IsRecyclable;
            IsDepositable = setting.IsDepositable;
        }
        else
        {
            var globalSetting = configProvider.Config.GetDenominationSetting(key);
            IsRecyclable = globalSetting.IsRecyclable;
            IsDepositable = globalSetting.IsDepositable;
        }

        Name = !string.IsNullOrEmpty(name) ? name : metadataProvider.GetDenominationName(key);
        Status = monitor.Status.ToBindableReactiveProperty();
        Count = new BindableReactiveProperty<int>(inventory.GetTotalCount(key));

        RecyclableCount = new BindableReactiveProperty<int>(inventory.GetCount(key));
        CollectionCount = new BindableReactiveProperty<int>(inventory.CollectionCounts.FirstOrDefault(x => x.Key == key).Value);
        RejectCount = new BindableReactiveProperty<int>(inventory.RejectCounts.FirstOrDefault(x => x.Key == key).Value);

        inventory.Changed
            .Where(k => k.Value == key.Value && k.Type == key.Type && k.CurrencyCode == key.CurrencyCode)
            .Subscribe(_ => SafeInvoke(() =>
            {
                Count.Value = inventory.GetTotalCount(key);
                RecyclableCount.Value = inventory.GetCount(key);
                CollectionCount.Value = inventory.CollectionCounts.FirstOrDefault(x => x.Key == key).Value;
                RejectCount.Value = inventory.RejectCounts.FirstOrDefault(x => x.Key == key).Value;
            }))
            .AddTo(_disposables);

        IsAcceptingCash = depositController.Changed
            .Select(_ => depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused)
            .ToBindableReactiveProperty(depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused);

        ShowDetailCommand = new ReactiveCommand().AddTo(_disposables);
    }

    private void SafeInvoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose() => _disposables.Dispose();
}
