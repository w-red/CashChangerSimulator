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

        GlobalModeName = Deposit.CurrentModeName
            .CombineLatest(Dispense.StatusName, (depositMode, dispenseMode) => 
            {
                if (dispenseMode == "Busy") return "DISPENSING (出金中)";
                return depositMode;
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
