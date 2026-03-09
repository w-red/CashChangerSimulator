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

    /// <summary>リサイクル（払い出し）可能かどうか。</summary>
    public bool IsRecyclable { get; }
    /// <summary>入金可能かどうか。</summary>
    public bool IsDepositable { get; }
    /// <summary>詳細情報を表示するコマンド。</summary>
    public ReactiveCommand<Unit> ShowDetailCommand { get; }

    /// <summary>依存関係を注入して DenominationViewModel を初期化します。</summary>
    /// <param name="inventory">在庫データ管理用インスタンス。</param>
    /// <param name="key">対象となる金種のキー情報。</param>
    /// <param name="metadataProvider">通貨や名称のメタデータプロバイダー。</param>
    /// <param name="depositController">入金処理のコントロール状態。</param>
    /// <param name="monitor">個別の金種状態監視モニター。</param>
    /// <param name="configProvider">アプリケーション設定プロバイダー。</param>
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

        var isAccepting = depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused;
        IsAcceptingCash = depositController.Changed
            .Select(_ => depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused)
            .ToBindableReactiveProperty(isAccepting);

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

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
