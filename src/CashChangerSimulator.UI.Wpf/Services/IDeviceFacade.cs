using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>デバイスやコアドメインの各種サービスへ一元的にアクセスするためのFacadeインターフェース。</summary>
public interface IDeviceFacade
{
    Inventory Inventory { get; }
    CashChangerManager Manager { get; }
    DepositController Deposit { get; }
    DispenseController Dispense { get; }
    HardwareStatusManager Status { get; }
    SimulatorCashChanger Changer { get; }
    TransactionHistory History { get; }
    OverallStatusAggregatorProvider AggregatorProvider { get; }
    MonitorsProvider Monitors { get; }
    INotifyService Notify { get; }
    IDispatcherService Dispatcher { get; }
    IViewService View { get; }
}
