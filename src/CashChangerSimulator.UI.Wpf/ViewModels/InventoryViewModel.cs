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
using System.Collections.Specialized;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金在庫の可視化とデバイスの基本操作（接続・エラー解除）を担当する ViewModel。</summary>
/// <remarks>
/// 各金種の在庫数、デバイス全体のステータス（センサー状態）、取引履歴の表示を集約します。
/// 設定の変更通知（MonitorsProvider等）を受けて、表示する金種リストを動的に更新します。
/// </remarks>
public class InventoryViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly INotifyService _notifyService;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>通貨記号のプレフィックス。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    /// <summary>通貨記号のサフィックス。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>紙幣金種ごとの ViewModel リスト（リサイクル可のみ）。</summary>
    public ObservableCollection<DenominationViewModel> BillDenominations { get; } = [];
    /// <summary>硬貨金種ごとの ViewModel リスト（リサイクル可のみ）。</summary>
    public ObservableCollection<DenominationViewModel> CoinDenominations { get; } = [];
    /// <summary>すべての金種 ViewModel リスト（読み取り専用）。</summary>
    public IEnumerable<DenominationViewModel> Denominations => BillDenominations.Concat(CoinDenominations);
    /// <summary>デバイス全体の在庫ステータス（空・ニアエンプティ）。</summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>デバイス全体の満杯ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }
    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReactiveProperty<bool> IsJammed { get; }
    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReactiveProperty<bool> IsOverlapped { get; }
    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }
    /// <summary>現在のエラーコード。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode { get; }
    /// <summary>デバイスが接続（Open）されているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsConnected { get; }
    public BindableReactiveProperty<bool> IsEmpty { get; }
    /// <summary>デバイスを物理的にオープンするコマンド。</summary>
    public ReactiveCommand OpenCommand { get; }
    /// <summary>デバイスを物理的にクローズするコマンド。</summary>
    public ReactiveCommand CloseCommand { get; }
    /// <summary>最近の取引履歴リスト。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];
    /// <summary>設定画面を開くコマンド。</summary>
    public ReactiveCommand OpenSettingsCommand { get; }
    /// <summary>エラー解除コマンド。</summary>
    public ReactiveCommand ResetErrorCommand { get; }
    /// <summary>すべての在庫を 0 にするコマンド。</summary>
    public ReactiveCommand CollectAllCommand { get; }
    /// <summary>すべての在庫を初期値に戻すコマンド。</summary>
    public ReactiveCommand ReplenishAllCommand { get; }
    /// <summary>金種の詳細ダイアログを表示するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> ShowDenominationDetailCommand { get; }

    /// <summary>必要なサービスを注入して InventoryViewModel を初期化します。</summary>
    /// <remarks>在庫データの監視、履歴の購読、および各コマンドのバインディング設定を行います。</remarks>
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
        _inventory = inventory;
        _history = history;
        _statusAggregator = aggregator;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _hardwareStatusManager = hardwareStatusManager;
        _notifyService = notifyService;
        CurrencyPrefix = _metadataProvider.SymbolPrefix;
        CurrencySuffix = _metadataProvider.SymbolSuffix;

        IsJammed = _hardwareStatusManager.IsJammed;
        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        IsDeviceError = _hardwareStatusManager.IsDeviceError;
        CurrentErrorCode = _hardwareStatusManager.CurrentErrorCode;
        IsConnected = _hardwareStatusManager.IsConnected;
        OverallStatus = _statusAggregator.DeviceStatus;
        FullStatus = _statusAggregator.FullStatus;

        BillGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        CoinGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);

        // Denominations initialization
        InitializeDenominations(depositController);

        // モニターリスト更新時にUI（金種表示）も再取得する
        _monitorsProvider.Changed
            .Subscribe(_ =>
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => InitializeDenominations(depositController));
                }
                else
                {
                    InitializeDenominations(depositController);
                }
            })
            .AddTo(_disposables);

        _history.Added
            .Subscribe(entry =>
            {
                void AddEntry()
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                    if (IsEmpty != null)
                    {
                        IsEmpty.Value = RecentTransactions.Count == 0;
                    }
                }

                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(AddEntry);
                }
                else
                {
                    AddEntry();
                }
            })
            .AddTo(_disposables);



        OpenSettingsCommand = new ReactiveCommand().AddTo(_disposables);
        OpenSettingsCommand.Subscribe(_ =>
        {
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            var settingsWindow = new SettingsWindow()
            {
                Owner = mainWindow
            };
            settingsWindow.ShowDialog();
        });

        ResetErrorCommand = new ReactiveCommand().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _hardwareStatusManager.ResetError());

        IsEmpty = new BindableReactiveProperty<bool>(RecentTransactions.Count == 0).AddTo(_disposables);

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

    private void InitializeDenominations(DepositController depositController)
    {
        BillDenominations.Clear();
        CoinDenominations.Clear();

        foreach (var monitor in _monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            var setting = _configProvider.Config.GetDenominationSetting(key);

            if (setting.IsRecyclable || setting.IsDepositable)
            {
                var vm = new DenominationViewModel(_inventory, key, _metadataProvider, depositController, monitor, _configProvider);
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
            // 金種数に応じた比率を設定
            BillGridWidth.Value = new GridLength(billCount, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(coinCount, GridUnitType.Star);
        }
    }

    /// <summary>紙幣エリアの Grid 幅比率。</summary>
    public BindableReactiveProperty<GridLength> BillGridWidth { get; }

    /// <summary>硬貨エリアの Grid 幅比率。</summary>
    public BindableReactiveProperty<GridLength> CoinGridWidth { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}

