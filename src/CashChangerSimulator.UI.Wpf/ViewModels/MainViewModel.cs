using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Views;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>アプリケーションのメイン画面を制御する ViewModel。</summary>
public class MainViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>入金管理用の ViewModel。</summary>
    public DepositViewModel Deposit { get; }
    /// <summary>出金管理用の ViewModel。</summary>
    public DispenseViewModel Dispense { get; }
    /// <summary>在庫管理用の ViewModel。</summary>
    public InventoryViewModel Inventory { get; }
    /// <summary>POS取引モード用の ViewModel。</summary>
    public PosTransactionViewModel PosTransaction { get; }
    /// <summary>高度なシミュレーション設定 ViewModel。</summary>
    public AdvancedSimulationViewModel AdvancedSimulation { get; }

    /// <summary>通貨記号。</summary>
    public string CurrencyPrefix { get; }
    public string CurrencySuffix { get; }

    /// <summary>現在の UI 動作モード。</summary>
    public BindableReactiveProperty<UIMode> CurrentUIMode { get; }

    /// <summary>入金ウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenDepositCommand { get; }
    /// <summary>出金ウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenDispenseCommand { get; }
    /// <summary>高度なシミュレーションウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenAdvancedSimulationCommand { get; }

    /// <summary>MainViewModel の新しいインスタンスを初期化します。</summary>
    public MainViewModel(
        Inventory inventory,
        TransactionHistory history,
        CashChangerManager manager,
        MonitorsProvider monitorsProvider,
        OverallStatusAggregatorProvider aggregatorProvider,
        ConfigurationProvider configProvider,
        CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        DispenseController dispenseController,
        SimulatorCashChanger cashChanger,
        INotifyService notifyService,
        IScriptExecutionService scriptExecutionService)
    {
        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        var isDispenseBusy = new BindableReactiveProperty<bool>(false).AddTo(_disposables);

        // Sub-ViewModels
        Inventory = new InventoryViewModel(
            inventory,
            history,
            aggregatorProvider.Aggregator,
            configProvider,
            monitorsProvider,
            metadataProvider,
            hardwareStatusManager,
            depositController)
            .AddTo(_disposables);

        Deposit = new DepositViewModel(
            depositController,
            hardwareStatusManager,
            () => Inventory.Denominations,
            isDispenseBusy,
            notifyService,
            metadataProvider)
            .AddTo(_disposables);

        Dispense = new DispenseViewModel(
            inventory,
            manager,
            dispenseController,
            hardwareStatusManager,
            configProvider,
            Deposit.IsInDepositMode,
            () => Inventory.Denominations,
            notifyService,
            metadataProvider)
            .AddTo(_disposables);

        Dispense.IsBusy.Subscribe(busy => isDispenseBusy.Value = busy).AddTo(_disposables);

        PosTransaction = new PosTransactionViewModel(Deposit, Dispense, cashChanger, metadataProvider).AddTo(_disposables);
        AdvancedSimulation = new AdvancedSimulationViewModel(cashChanger, scriptExecutionService, depositController, metadataProvider).AddTo(_disposables);

        CurrentUIMode = new BindableReactiveProperty<UIMode>(configProvider.Config.System.UIMode).AddTo(_disposables);

        configProvider.Reloaded
            .Subscribe(_ => CurrentUIMode.Value = configProvider.Config.System.UIMode)
            .AddTo(_disposables);

        GlobalModeName = Deposit.CurrentModeName
            .CombineLatest(Dispense.StatusName, (depositMode, dispenseMode) =>
            {
                return dispenseMode == "Busy"
                    ? "DISPENSING" : depositMode;
            })
            .ToBindableReactiveProperty("IDLE")
            .AddTo(_disposables);

        OpenDepositCommand = new ReactiveCommand().AddTo(_disposables);
        OpenDepositCommand.Subscribe(_ =>
        {
            if (hardwareStatusManager.IsJammed.Value || hardwareStatusManager.IsOverlapped.Value)
            {
                notifyService.ShowWarning(
                    (string)System.Windows.Application.Current.Resources["StrErrorCannotOpenTerminalInError"],
                    (string)System.Windows.Application.Current.Resources["StrWarn"]);
                return;
            }

            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                var window = new DepositWindow(Deposit, () => Inventory.Denominations) { Owner = mainWindow };
                window.Show();
            }
            else
            {
                var window = new DepositWindow(Deposit, () => Inventory.Denominations);
                window.Show();
            }
        });

        OpenDispenseCommand = new ReactiveCommand().AddTo(_disposables);
        OpenDispenseCommand.Subscribe(_ =>
        {
            if (hardwareStatusManager.IsJammed.Value || hardwareStatusManager.IsOverlapped.Value)
            {
                notifyService.ShowWarning(
                    (string)System.Windows.Application.Current.Resources["StrErrorCannotOpenTerminalInError"],
                    (string)System.Windows.Application.Current.Resources["StrWarn"]);
                return;
            }

            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                var window = new DispenseWindow(Dispense, () => Inventory.Denominations) { Owner = mainWindow };
                window.Show();
            }
            else
            {
                var window = new DispenseWindow(Dispense, () => Inventory.Denominations);
                window.Show();
            }
        });

        OpenAdvancedSimulationCommand = new ReactiveCommand().AddTo(_disposables);
        OpenAdvancedSimulationCommand.Subscribe(_ =>
        {
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            var window = new AdvancedSimulationWindow(AdvancedSimulation) { Owner = mainWindow };
            window.Show();
        });
    }

    /// <summary>全体的な動作状態の表示名。</summary>
    public BindableReactiveProperty<string> GlobalModeName { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}

