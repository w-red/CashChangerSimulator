using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary><see cref="IDeviceFacade"/> の実装。</summary>
public class DeviceFacade : IDeviceFacade
{
    public Inventory Inventory { get; }
    public CashChangerManager Manager { get; }
    public DepositController Deposit { get; }
    public DispenseController Dispense { get; }
    public HardwareStatusManager Status { get; }
    public SimulatorCashChanger Changer { get; }
    public TransactionHistory History { get; }
    public OverallStatusAggregatorProvider AggregatorProvider { get; }
    public MonitorsProvider Monitors { get; }
    public INotifyService Notify { get; }
    public IDispatcherService Dispatcher { get; }

    public DeviceFacade(
        Inventory inventory,
        CashChangerManager manager,
        DepositController deposit,
        DispenseController dispense,
        HardwareStatusManager status,
        SimulatorCashChanger changer,
        TransactionHistory history,
        OverallStatusAggregatorProvider aggregatorProvider,
        MonitorsProvider monitors,
        INotifyService notify,
        IDispatcherService dispatcher)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(deposit);
        ArgumentNullException.ThrowIfNull(dispense);
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(changer);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(aggregatorProvider);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(notify);
        ArgumentNullException.ThrowIfNull(dispatcher);

        Inventory = inventory;
        Manager = manager;
        Deposit = deposit;
        Dispense = dispense;
        Status = status;
        Changer = changer;
        History = history;
        AggregatorProvider = aggregatorProvider;
        Monitors = monitors;
        Notify = notify;
        Dispatcher = dispatcher;
    }
}
