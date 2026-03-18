using CashChangerSimulator.Core;
using Microsoft.Extensions.Logging;
using ZLogger;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.Views;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金在庫の可視化とデバイスの基本操作（接続・エラー解除）を担当する ViewModel。</summary>
public class InventoryViewModel : IDisposable
{
    private readonly ILogger<InventoryViewModel> _logger = LogProvider.CreateLogger<InventoryViewModel>();
    private readonly IDeviceFacade _facade;
    private readonly ConfigurationProvider _configProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly INotifyService _notifyService;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>通貨の接頭辞（例: ￥）。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    /// <summary>通貨の接尾辞。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>紙幣金種のリスト。</summary>
    public ObservableCollection<DenominationViewModel> BillDenominations { get; } = [];
    /// <summary>硬貨金種のリスト。</summary>
    public ObservableCollection<DenominationViewModel> CoinDenominations { get; } = [];
    /// <summary>全金種の列挙。</summary>
    public IEnumerable<DenominationViewModel> Denominations => BillDenominations.Concat(CoinDenominations);

    /// <summary>デバイス全体の在庫ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>フル状態のステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }
    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReactiveProperty<bool> IsJammed { get; }
    /// <summary>重なりが発生しているかどうか。</summary>
    public ReactiveProperty<bool> IsOverlapped { get; }
    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }
    /// <summary>現在のエラーコード。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode { get; }
    /// <summary>デバイスと接続されているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsConnected { get; }

    /// <summary>取引履歴が空かどうか。</summary>
    public BindableReactiveProperty<bool> IsEmpty { get; }
    /// <summary>UI上の紙幣エリアの幅割合。</summary>
    public BindableReactiveProperty<GridLength> BillGridWidth { get; }
    /// <summary>UI上の硬貨エリアの幅割合。</summary>
    public BindableReactiveProperty<GridLength> CoinGridWidth { get; }

    /// <summary>デバイスをオープンするコマンド。</summary>
    public ReactiveCommand OpenCommand { get; }
    /// <summary>デバイスをクローズするコマンド。</summary>
    public ReactiveCommand CloseCommand { get; }
    /// <summary>設定画面を表示するコマンド。</summary>
    public ReactiveCommand OpenSettingsCommand { get; }
    /// <summary>エラーをリセットするコマンド。</summary>
    public ReactiveCommand ResetErrorCommand { get; }
    /// <summary>全在庫を回収するコマンド。</summary>
    public ReactiveCommand CollectAllCommand { get; }
    /// <summary>全在庫を補充するコマンド。</summary>
    public ReactiveCommand ReplenishAllCommand { get; }
    /// <summary>金種詳細を表示するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> ShowDenominationDetailCommand { get; }


    /// <summary>最近の取引履歴。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];

    /// <summary>依存関係を注入して InventoryViewModel を初期化します。</summary>
    /// <param name="facade">デバイスとコア機能の Facade。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    /// <param name="notifyService">通知サービス。</param>
    public InventoryViewModel(
        IDeviceFacade facade,
        ConfigurationProvider configProvider,
        CurrencyMetadataProvider metadataProvider,
        INotifyService notifyService)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(notifyService);

        _facade = facade;
        _configProvider = configProvider;
        _metadataProvider = metadataProvider;
        _notifyService = notifyService;

        CurrencyPrefix = _metadataProvider.SymbolPrefix;
        CurrencySuffix = _metadataProvider.SymbolSuffix;
        OverallStatus = _facade.AggregatorProvider.Aggregator.DeviceStatus;
        FullStatus = _facade.AggregatorProvider.Aggregator.FullStatus;
        IsJammed = _facade.Status.IsJammed;
        IsOverlapped = _facade.Status.IsOverlapped;
        IsDeviceError = _facade.Status.IsDeviceError;
        CurrentErrorCode = _facade.Status.CurrentErrorCode;
        IsConnected = _facade.Status.IsConnected;

        BillGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        CoinGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        IsEmpty = new BindableReactiveProperty<bool>(RecentTransactions.Count == 0).AddTo(_disposables);

        SafeInvoke(InitializeDenominations);

        _facade.Monitors.Changed
            .Subscribe(_ => SafeInvoke(InitializeDenominations))
            .AddTo(_disposables);

        _facade.History.Added
            .Subscribe(entry => SafeInvoke(() =>
            {
                RecentTransactions.Insert(0, entry);
                if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                IsEmpty.Value = RecentTransactions.Count == 0;
            }))
            .AddTo(_disposables);

        OpenSettingsCommand = new ReactiveCommand().AddTo(_disposables);
        OpenSettingsCommand.Subscribe(_ =>
        {
            _facade.View.ShowSettingsWindow();
        });

        ResetErrorCommand = new ReactiveCommand().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _facade.Status.ResetError());

        OpenCommand = new ReactiveCommand().AddTo(_disposables);
        OpenCommand.Subscribe(_ =>
        {
            try
            {
                _facade.Changer.Open();
                if (_facade.Changer.SkipStateVerification)
                {
                    _facade.Changer.Claim(0);
                    _facade.Changer.DeviceEnabled = true;
                }
            }
            catch (Exception ex)
            {
                _facade.Status.SetDeviceError((int)ErrorCode.Failure);
                _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
            }
        });

        CloseCommand = new ReactiveCommand().AddTo(_disposables);
        CloseCommand.Subscribe(_ =>
        {
            try
            {
                _facade.Changer.Close();
            }
            catch (Exception ex)
            {
                _facade.Status.SetDeviceError((int)ErrorCode.Failure);
                _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
            }
        });

        CollectAllCommand = new ReactiveCommand().AddTo(_disposables);
        CollectAllCommand.Subscribe(_ =>
        {
            foreach (var monitor in _facade.Monitors.Monitors)
            {
                _facade.Inventory.SetCount(monitor.Key, 0);
            }
        });

        ReplenishAllCommand = new ReactiveCommand().AddTo(_disposables);
        ReplenishAllCommand.Subscribe(_ =>
        {
            foreach (var monitor in _facade.Monitors.Monitors)
            {
                var setting = _configProvider.Config.GetDenominationSetting(monitor.Key);
                _facade.Inventory.SetCount(monitor.Key, setting.InitialCount);
            }
        });

        ShowDenominationDetailCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
        ShowDenominationDetailCommand.Subscribe(vm =>
        {
            if (vm == null) return;
            SafeInvoke(() =>
            {
                var view = new DenominationDetailView { DataContext = vm };
                _ = _facade.View.ShowDialogAsync(view, "RootDialog");
            });
        });

    }

    private void SafeInvoke(Action action)
    {
        _facade.Dispatcher.SafeInvoke(action);
    }

    private void InitializeDenominations()
    {
        BillDenominations.Clear();
        CoinDenominations.Clear();

        _logger.ZLogDebug($"InitializeDenominations: Found {_facade.Monitors.Monitors.Count()} monitors.");
        foreach (var monitor in _facade.Monitors.Monitors)
        {
            var key = monitor.Key;
            var setting = _configProvider.Config.GetDenominationSetting(key);
            _logger.ZLogDebug($"InitializeDenominations: Denom {key} - IsRecyclable: {setting.IsRecyclable}, IsDepositable: {setting.IsDepositable}");

            if (setting.IsRecyclable || setting.IsDepositable)
            {
                var vm = new DenominationViewModel(_facade, key, _metadataProvider, monitor, _configProvider);
                vm.ShowDetailCommand.Subscribe(x => ShowDenominationDetailCommand.Execute(x)).AddTo(_disposables);
                if (key.Type == CurrencyCashType.Bill)
                {
                    _logger.ZLogDebug($"InitializeDenominations: Adding Bill {key}");
                    BillDenominations.Add(vm);
                }
                else
                {
                    _logger.ZLogDebug($"InitializeDenominations: Adding Coin {key}");
                    CoinDenominations.Add(vm);
                }
            }
        }
        _logger.ZLogDebug($"InitializeDenominations: Finished. Bills: {BillDenominations.Count}, Coins: {CoinDenominations.Count}");

        UpdateGridRatios();
    }

    private void UpdateGridRatios()
    {
        int billCount = BillDenominations.Count;
        int coinCount = CoinDenominations.Count;

        if (billCount == 0 && coinCount == 0)
        {
            BillGridWidth.Value = new GridLength(1, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(1, GridUnitType.Star);
        }
        else if (billCount == 0)
        {
            BillGridWidth.Value = new GridLength(0, GridUnitType.Pixel); // Use Pixel 0 instead of Star 0
            CoinGridWidth.Value = new GridLength(1, GridUnitType.Star);
        }
        else if (coinCount == 0)
        {
            BillGridWidth.Value = new GridLength(1, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(0, GridUnitType.Pixel); // Use Pixel 0 instead of Star 0
        }
        else
        {
            BillGridWidth.Value = new GridLength(billCount, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(coinCount, GridUnitType.Star);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
