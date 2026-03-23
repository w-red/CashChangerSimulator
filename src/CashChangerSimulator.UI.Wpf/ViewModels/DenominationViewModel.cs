using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.PointOfService;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>個別の金種（1000円、100円等）の表示状態と操作を管理する ViewModel。</summary>
public class DenominationViewModel : IDisposable
{
    private readonly IDispatcherService _dispatcher;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>金種のキー情報。</summary>
    public DenominationKey Key { get; }
    /// <summary>表示名称。</summary>
    public string Name { get; }
    /// <summary>合計枚数（リサイクル＋回収）。</summary>
    public BindableReactiveProperty<int> Count { get; }
    /// <summary>金種の状態（ニアフル、フル等）。</summary>
    public BindableReactiveProperty<CashStatus> Status { get; }
    /// <summary>現在入金を受け入れ可能かどうか。</summary>
    public BindableReactiveProperty<bool> IsAcceptingCash { get; }

    /// <summary>リサイクル庫の枚数。</summary>
    public BindableReactiveProperty<int> RecyclableCount { get; }
    /// <summary>回収庫の枚数。</summary>
    public BindableReactiveProperty<int> CollectionCount { get; }
    /// <summary>リジェクト庫の枚数。</summary>
    public BindableReactiveProperty<int> RejectCount { get; }
    /// <summary>入金トレイ（エスクロー）の枚数。</summary>
    public BindableReactiveProperty<int> EscrowCount { get; }

    /// <summary>リサイクル（払い出し）可能かどうか。</summary>
    public bool IsRecyclable { get; }
    /// <summary>入金可能かどうか。</summary>
    public bool IsDepositable { get; }
    /// <summary>エスクローがあるかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> HasEscrow { get; }

    /// <summary>詳細情報を表示するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> ShowDetailCommand { get; }

    // --- ローカライズされた文字列 ---
    public string LabelInventoryDetail { get; }
    public string LabelRecyclable { get; }
    public string LabelCollection { get; }
    public string LabelReject { get; }
    public string LabelEscrow { get; }
    public string LabelTotalCount { get; }
    public string SuffixCount { get; }
    public string LabelNoteNonRecyclable { get; }

    /// <summary>依存関係を注入して DenominationViewModel を初期化します。</summary>
    /// <param name="facade">デバイスとコア機能の Facade。</param>
    /// <param name="key">対象となる金種のキー情報。</param>
    /// <param name="metadataProvider">通貨や名称のメタデータプロバイダー。</param>
    /// <param name="monitor">個別の金種状態監視モニター。</param>
    /// <param name="configProvider">アプリケーション設定プロバイダー。</param>
    public DenominationViewModel(
        IDeviceFacade facade,
        DenominationKey key,
        CurrencyMetadataProvider metadataProvider,
        CashStatusMonitor monitor,
        ConfigurationProvider configProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(configProvider);
        
        _dispatcher = facade.Dispatcher;
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
        Count = new BindableReactiveProperty<int>(facade.Inventory.GetTotalCount(key));

        RecyclableCount = new BindableReactiveProperty<int>(facade.Inventory.GetCount(key));
        CollectionCount = new BindableReactiveProperty<int>(facade.Inventory.CollectionCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value);
        RejectCount = new BindableReactiveProperty<int>(facade.Inventory.RejectCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value);
        EscrowCount = new BindableReactiveProperty<int>(facade.Inventory.EscrowCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value);
        HasEscrow = EscrowCount.Select(x => x > 0).ToReadOnlyReactiveProperty().AddTo(_disposables);

        // ローカライズ文字列の初期化
        LabelInventoryDetail = ResourceHelper.GetAsString("InventoryDetail", "INVENTORY DETAIL");
        LabelRecyclable = ResourceHelper.GetAsString("Recyclable", "RECYCLABLE");
        LabelCollection = ResourceHelper.GetAsString("Collection", "COLLECTION");
        LabelReject = ResourceHelper.GetAsString("Reject", "REJECT");
        LabelEscrow = ResourceHelper.GetAsString("LabelPendingItems", "ESCROW");
        LabelTotalCount = ResourceHelper.GetAsString("TotalCountLabel", "TOTAL COUNT");
        SuffixCount = ResourceHelper.GetAsString("CountSuffix", "");
        LabelNoteNonRecyclable = ResourceHelper.GetAsString("NoteNonRecyclable", "This denomination is non-recyclable...");

        facade.Inventory.Changed
            .Where(k => k.Value == key.Value && k.Type == key.Type && k.CurrencyCode == key.CurrencyCode)
            .Subscribe(_ => SafeInvoke(() =>
            {
                Count.Value = facade.Inventory.GetTotalCount(key);
                RecyclableCount.Value = facade.Inventory.GetCount(key);
                CollectionCount.Value = facade.Inventory.CollectionCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value;
                RejectCount.Value = facade.Inventory.RejectCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value;
                EscrowCount.Value = facade.Inventory.EscrowCounts.FirstOrDefault(x => x.Key.Value == key.Value && x.Key.Type == key.Type).Value;
            }))
            .AddTo(_disposables);

        var isAccepting = facade.Deposit.DepositStatus == CashDepositStatus.Count && !facade.Deposit.IsFixed && !facade.Deposit.IsPaused;
        IsAcceptingCash = facade.Deposit.Changed
            .Select(_ => facade.Deposit.DepositStatus == CashDepositStatus.Count && !facade.Deposit.IsFixed && !facade.Deposit.IsPaused)
            .ToBindableReactiveProperty(isAccepting);

        ShowDetailCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
    }

    private void SafeInvoke(Action action)
    {
        _dispatcher.SafeInvoke(action);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
