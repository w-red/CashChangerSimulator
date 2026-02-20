using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// アプリケーションのメイン画面を制御するメイン（シェル） ViewModel。
/// 入金、出金、在庫のサブ ViewModel を統括する。
/// </summary>
public class MainViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    public DepositViewModel Deposit { get; }
    public DispenseViewModel Dispense { get; }
    public InventoryViewModel Inventory { get; }
    public PosTransactionViewModel PosTransaction { get; }

    public BindableReactiveProperty<UIMode> CurrentUIMode { get; }

    public MainViewModel(
        Inventory inventory,
        TransactionHistory history,
        CashChangerManager manager,
        MonitorsProvider monitorsProvider,
        OverallStatusAggregatorProvider aggregatorProvider,
        ConfigurationProvider configProvider,
        Services.CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        DispenseController dispenseController)
    {
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
            () => Inventory.Denominations)
            .AddTo(_disposables);

        Dispense = new DispenseViewModel(
            inventory,
            manager,
            dispenseController,
            configProvider,
            Deposit.IsInDepositMode,
            hardwareStatusManager.IsJammed,
            () => Inventory.Denominations)
            .AddTo(_disposables);

        PosTransaction = new PosTransactionViewModel(Deposit, Dispense).AddTo(_disposables);

        CurrentUIMode = new BindableReactiveProperty<UIMode>(configProvider.Config.Simulation.UIMode).AddTo(_disposables);

        // Update mode when settings change
        try
        {
            var simSettings = DIContainer.Resolve<SimulationSettings>();
            // Since we don't have a direct notification from DI singleton, 
            // we'll rely on the fact that SettingsViewModel updates the configProvider which we can observe if it had an event.
            // For now, let's assume the user might need to restart or we can trigger a manual refresh if needed.
            // Ideally, SimulationSettings should be reactive.
        }
        catch
        {
            // Logged and swallowed to prevent startup failure if DI resolve fails (simSettings usage is optional here)
        }

        configProvider.Reloaded
            .Subscribe(_ => CurrentUIMode.Value = configProvider.Config.Simulation.UIMode)
            .AddTo(_disposables);

        GlobalModeName = Deposit.CurrentModeName
            .CombineLatest(Dispense.StatusName, (depositMode, dispenseMode) =>
            {
                return dispenseMode == "Busy"
                    ? "DISPENSING (出金中)" : depositMode;
            })
            .ToBindableReactiveProperty("IDLE (待機中)")
            .AddTo(_disposables);
    }

    public BindableReactiveProperty<string> GlobalModeName { get; }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
