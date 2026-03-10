using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Testing;
using CashChangerSimulator.UI.Wpf.Views;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金在庫の可視化とデバイスの基本操作（接続・エラー解除）を担当する ViewModel。</summary>
public class InventoryViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DepositController _depositController;
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
    /// <param name="inventory">在庫管理インスタンス。</param>
    /// <param name="history">取引履歴サービス。</param>
    /// <param name="aggregator">ステータス集約プロバイダー。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="monitorsProvider">監視モニタープロバイダー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態マネージャー。</param>
    /// <param name="depositController">入金コントローラー。</param>
    /// <param name="cashChanger">デバイス本体インスタンス。</param>
    /// <param name="notifyService">通知サービス。</param>
    public InventoryViewModel(
        Inventory inventory,
        TransactionHistory history,
        OverallStatusAggregator aggregator,
        ConfigurationProvider configProvider,
        MonitorsProvider monitorsProvider,
        CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        SimulatorCashChanger cashChanger,
        INotifyService notifyService)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(aggregator);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(monitorsProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(hardwareStatusManager);
        ArgumentNullException.ThrowIfNull(depositController);
        ArgumentNullException.ThrowIfNull(cashChanger);
        ArgumentNullException.ThrowIfNull(notifyService);

        _inventory = inventory;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _hardwareStatusManager = hardwareStatusManager;
        _depositController = depositController;
        _notifyService = notifyService;

        CurrencyPrefix = _metadataProvider.SymbolPrefix;
        CurrencySuffix = _metadataProvider.SymbolSuffix;
        OverallStatus = aggregator.DeviceStatus;
        FullStatus = aggregator.FullStatus;
        IsJammed = _hardwareStatusManager.IsJammed;
        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        IsDeviceError = _hardwareStatusManager.IsDeviceError;
        CurrentErrorCode = _hardwareStatusManager.CurrentErrorCode;
        IsConnected = _hardwareStatusManager.IsConnected;

        BillGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        CoinGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        IsEmpty = new BindableReactiveProperty<bool>(RecentTransactions.Count == 0).AddTo(_disposables);

        InitializeDenominations();

        _monitorsProvider.Changed
            .Subscribe(_ => SafeInvoke(InitializeDenominations))
            .AddTo(_disposables);

        history.Added
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
            var mainWindow = Application.Current?.MainWindow;
            new SettingsWindow { Owner = mainWindow }.ShowDialog();
        });

        ResetErrorCommand = new ReactiveCommand().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _hardwareStatusManager.ResetError());

        OpenCommand = new ReactiveCommand().AddTo(_disposables);
        OpenCommand.Subscribe(_ =>
        {
            try
            {
                cashChanger.Open();
                if (cashChanger.SkipStateVerification)
                {
                    cashChanger.Claim(0);
                    cashChanger.DeviceEnabled = true;
                }
            }
            catch (Exception ex)
            {
                _hardwareStatusManager.SetDeviceError((int)ErrorCode.Failure);
                _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
            }
        });

        CloseCommand = new ReactiveCommand().AddTo(_disposables);
        CloseCommand.Subscribe(_ =>
        {
            try
            {
                cashChanger.Close();
            }
            catch (Exception ex)
            {
                _hardwareStatusManager.SetDeviceError((int)ErrorCode.Failure);
                _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
            }
        });

        CollectAllCommand = new ReactiveCommand().AddTo(_disposables);
        CollectAllCommand.Subscribe(_ =>
        {
            foreach (var monitor in _monitorsProvider.Monitors)
            {
                _inventory.SetCount(monitor.Key, 0);
            }
        });

        ReplenishAllCommand = new ReactiveCommand().AddTo(_disposables);
        ReplenishAllCommand.Subscribe(_ =>
        {
            foreach (var monitor in _monitorsProvider.Monitors)
            {
                var setting = _configProvider.Config.GetDenominationSetting(monitor.Key);
                _inventory.SetCount(monitor.Key, setting.InitialCount);
            }
        });

        ShowDenominationDetailCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
        ShowDenominationDetailCommand.Subscribe(vm =>
        {
            if (vm != null)
            {
                var view = new DenominationDetailView { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(view, "RootDialog");
            }
        });
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

    private void InitializeDenominations()
    {
        BillDenominations.Clear();
        CoinDenominations.Clear();

        foreach (var monitor in _monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            var setting = _configProvider.Config.GetDenominationSetting(key);

            if (setting.IsRecyclable || setting.IsDepositable)
            {
                var vm = new DenominationViewModel(_inventory, key, _metadataProvider, _depositController, monitor, _configProvider);
                vm.ShowDetailCommand.Subscribe(_ => ShowDenominationDetailCommand.Execute(vm)).AddTo(_disposables);
                if (key.Type == CurrencyCashType.Bill)
                {
                    BillDenominations.Add(vm);
                }
                else
                {
                    CoinDenominations.Add(vm);
                }
            }
        }

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
            BillGridWidth.Value = new GridLength(0);
            CoinGridWidth.Value = new GridLength(1, GridUnitType.Star);
        }
        else if (coinCount == 0)
        {
            BillGridWidth.Value = new GridLength(1, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(0);
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
