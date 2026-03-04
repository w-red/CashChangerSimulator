using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Views;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>在庫表示とデバイスステータスを管理する ViewModel。</summary>
public class InventoryViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>通貨記号のプレフィックス。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    /// <summary>通貨記号のサフィックス。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>金種ごとの ViewModel リスト。</summary>
    public ObservableCollection<DenominationViewModel> Denominations { get; } = [];
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

    /// <summary>InventoryViewModel の新しいインスタンスを初期化します。</summary>
    public InventoryViewModel(
        Inventory inventory,
        TransactionHistory history,
        OverallStatusAggregator aggregator,
        ConfigurationProvider configProvider,
        MonitorsProvider monitorsProvider,
        CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        SimulatorCashChanger cashChanger)
    {
        _inventory = inventory;
        _history = history;
        _statusAggregator = aggregator;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _hardwareStatusManager = hardwareStatusManager;
        CurrencyPrefix = _metadataProvider.SymbolPrefix;
        CurrencySuffix = _metadataProvider.SymbolSuffix;

        IsJammed = _hardwareStatusManager.IsJammed;
        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        IsDeviceError = _hardwareStatusManager.IsDeviceError;
        CurrentErrorCode = _hardwareStatusManager.CurrentErrorCode;
        IsConnected = _hardwareStatusManager.IsConnected;
        OverallStatus = _statusAggregator.DeviceStatus;
        FullStatus = _statusAggregator.FullStatus;

        // Denominations initialization
        foreach (var monitor in monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, depositController, monitor, _configProvider));
        }

        _history.Added
            .Subscribe(entry =>
            {
                void AddEntry()
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
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
            var settingsWindow = new SettingsWindow()
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        });

        ResetErrorCommand = new ReactiveCommand().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _hardwareStatusManager.ResetError());

        OpenCommand = new ReactiveCommand().AddTo(_disposables);
        OpenCommand.Subscribe(_ =>
        {
            try
            {
                cashChanger.Open();
            }
            catch (Exception)
            {
                _hardwareStatusManager.SetDeviceError((int)ErrorCode.Failure);
            }
        });

        CloseCommand = new ReactiveCommand().AddTo(_disposables);
        CloseCommand.Subscribe(_ =>
        {
            try
            {
                cashChanger.Close();
            }
            catch (Exception)
            {
                _hardwareStatusManager.SetDeviceError((int)ErrorCode.Failure);
            }
        });
        
        CollectAllCommand = new ReactiveCommand().AddTo(_disposables);
        CollectAllCommand.Subscribe(_ =>
        {
            foreach (var den in Denominations)
            {
                _inventory.SetCount(den.Key, 0);
            }
        });

        ReplenishAllCommand = new ReactiveCommand().AddTo(_disposables);
        ReplenishAllCommand.Subscribe(_ =>
        {
            foreach (var den in Denominations)
            {
                var setting = _configProvider.Config.GetDenominationSetting(den.Key);
                _inventory.SetCount(den.Key, setting.InitialCount);
            }
        });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}

