using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;
using System.Linq;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>個別の金種（1000円、100円等）の表示状態と操作を管理する ViewModel。</summary>
/// <remarks>
/// 特定の金種の名称、在庫枚数、およびステータス（センサー状態）を保持します。
/// 在庫の変更を監視し、UI 上の表示枚数を最新の状態に保つ役割を担います。
/// </remarks>
public class DenominationViewModel
{
    private readonly Inventory _inventory;
    /// <summary>金種キー。</summary>
    public DenominationKey Key { get; }
    /// <summary>表示名。</summary>
    public string Name { get; }
    private readonly BindableReactiveProperty<int> _count;
    /// <summary>現在の在庫枚数。</summary>
    public BindableReactiveProperty<int> Count { get; }
    /// <summary>現在の在庫ステータス。</summary>
    public BindableReactiveProperty<CashStatus> Status { get; }
    /// <summary>現在この金種を受け入れ可能かどうか。</summary>
    public BindableReactiveProperty<bool> IsAcceptingCash { get; }

    /// <summary>還流庫（通常庫）の枚数。</summary>
    public BindableReactiveProperty<int> RecyclableCount { get; }
    /// <summary>回収庫（オーバーフロー）の枚数。</summary>
    public BindableReactiveProperty<int> CollectionCount { get; }
    /// <summary>リジェクト庫（汚損・不明）の枚数。</summary>
    public BindableReactiveProperty<int> RejectCount { get; }

    /// <summary>リサイクル可能（還流）かどうか。</summary>
    public bool IsRecyclable { get; }
    /// <summary>入金可能かどうか。</summary>
    public bool IsDepositable { get; }

    /// <summary>金種情報と監視オブジェクトを注入して DenominationViewModel を初期化します。</summary>
    /// <remarks>言語設定に基づいた表示名の決定や、在庫変更の購読設定を行います。</remarks>
    public DenominationViewModel(Inventory inventory, DenominationKey key, CurrencyMetadataProvider metadataProvider, DepositController depositController, CashStatusMonitor monitor, ConfigurationProvider configProvider)
    {
        _inventory = inventory;
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
        _count = new BindableReactiveProperty<int>(_inventory.GetTotalCount(key));
        Count = _count;

        RecyclableCount = new BindableReactiveProperty<int>(_inventory.GetCount(key));
        CollectionCount = new BindableReactiveProperty<int>(((IReadOnlyInventory)_inventory).CollectionCounts.FirstOrDefault(x => x.Key == key).Value);
        RejectCount = new BindableReactiveProperty<int>(((IReadOnlyInventory)_inventory).RejectCounts.FirstOrDefault(x => x.Key == key).Value);

        _inventory.Changed
            .Where(k => k.Value == key.Value && k.Type == key.Type && k.CurrencyCode == key.CurrencyCode)
            .Subscribe(_ =>
            {
                var newTotal = _inventory.GetTotalCount(key);
                var newRecyclable = _inventory.GetCount(key);
                var newCollection = ((IReadOnlyInventory)_inventory).CollectionCounts.FirstOrDefault(x => x.Key == key).Value;
                var newReject = ((IReadOnlyInventory)_inventory).RejectCounts.FirstOrDefault(x => x.Key == key).Value;

                System.Diagnostics.Debug.WriteLine($"[DenominationViewModel] Updated {key}: {newTotal} (Rec:{newRecyclable}, Col:{newCollection}, Rej:{newReject})");
                
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _count.Value = newTotal;
                        RecyclableCount.Value = newRecyclable;
                        CollectionCount.Value = newCollection;
                        RejectCount.Value = newReject;
                    });
                }
                else
                {
                    _count.Value = newTotal;
                    RecyclableCount.Value = newRecyclable;
                    CollectionCount.Value = newCollection;
                    RejectCount.Value = newReject;
                }
            });

        IsAcceptingCash = depositController.Changed
            .Select(_ => depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused)
            .ToBindableReactiveProperty(depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused);
    }
}

