using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests.Fixtures;

public class StateConflictTestFixture : IDisposable
{
    public Inventory Inventory { get; private set; } = null!;
    public TransactionHistory History { get; private set; } = null!;
    public CashChangerManager Manager { get; private set; } = null!;
    public HardwareStatusManager HardwareManager { get; private set; } = null!;
    public DepositController DepositController { get; private set; } = null!;
    public ConfigurationProvider ConfigProvider { get; private set; } = null!;
    public CurrencyMetadataProvider MetadataProvider { get; private set; } = null!;
    public MonitorsProvider MonitorsProvider { get; private set; } = null!;
    public OverallStatusAggregatorProvider AggregatorProvider { get; private set; } = null!;
    
    public Mock<DispenseController> MockDispenseController { get; private set; } = null!;
    public Mock<SimulatorCashChanger> MockCashChanger { get; } = new();
    public Mock<INotifyService> MockNotify { get; } = new();
    
    public Subject<Unit> DispenseChanged { get; } = new();

    public void Initialize()
    {
        Inventory = new Inventory();
        History = new TransactionHistory();
        Manager = new CashChangerManager(Inventory, History, new ChangeCalculator());
        HardwareManager = new HardwareStatusManager();
        DepositController = new DepositController(Inventory);
        ConfigProvider = new ConfigurationProvider();
        MetadataProvider = new CurrencyMetadataProvider(ConfigProvider);
        MonitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        AggregatorProvider = new OverallStatusAggregatorProvider(MonitorsProvider);

        MockDispenseController = new Mock<DispenseController>(Manager, HardwareManager, new Mock<IDeviceSimulator>().Object);
        MockDispenseController.SetupGet(c => c.Changed).Returns(DispenseChanged);
        MockDispenseController.SetupGet(c => c.IsBusy).Returns(false);
        
        MockCashChanger.SetupGet(c => c.CurrencyCode).Returns("JPY");
    }

    public void Dispose()
    {
        DispenseChanged.Dispose();
    }
}
