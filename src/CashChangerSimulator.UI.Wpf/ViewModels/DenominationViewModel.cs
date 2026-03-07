using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;

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
        }

        Name = !string.IsNullOrEmpty(name) ? name : metadataProvider.GetDenominationName(key);
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

