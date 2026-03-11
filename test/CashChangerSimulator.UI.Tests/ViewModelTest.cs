using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>ViewModel 全体の基本動作や初期状態を検証するテストクラス。</summary>
public class ViewModelTest
{
    /// <summary>MainViewModel の初期状態が正しくセットアップされることを検証します。</summary>
    [Fact]
    public void MainViewModelShouldInitializeCorrectly()
    {
        // Setup
        var mockInventory = new Mock<Inventory>();
        mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        mockInventory.Setup(i => i.CalculateTotal(It.IsAny<string>())).Returns(10000m);

        var mockHistory = new Mock<TransactionHistory>();
        mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        var realConfig = new ConfigurationProvider();
        realConfig.Config.System.CurrencyCode = "JPY";

        var realMetadata = new CurrencyMetadataProvider(realConfig);
        var realMonitors = new MonitorsProvider(mockInventory.Object, realConfig, realMetadata);
        var realAggregator = new OverallStatusAggregatorProvider(realMonitors);

        var mockManager = new Mock<CashChangerManager>(mockInventory.Object, mockHistory.Object, new ChangeCalculator());

        var realHardware = new HardwareStatusManager();
        var depositController = new DepositController(mockInventory.Object, realHardware);
        var mockSimulator = new Mock<IDeviceSimulator>();
        var dispenseController = new DispenseController(mockManager.Object, realHardware, mockSimulator.Object);

        var changer = new InternalSimulatorCashChanger(new SimulatorDependencies(
            realConfig, mockInventory.Object, mockHistory.Object, mockManager.Object, 
            depositController, dispenseController, realAggregator, realHardware));

        var facade = new DeviceFacade(
            mockInventory.Object,
            mockManager.Object,
            depositController,
            dispenseController,
            realHardware,
            changer,
            mockHistory.Object,
            realAggregator,
            realMonitors,
            new Mock<INotifyService>().Object);

        var services = new ServiceCollection();
        // ViewModels are usually singletons in standard registration, but for testing we can just register them or let ActivatorUtilities handle them.
        services.AddSingleton(facade);
        services.AddSingleton(realConfig);
        services.AddSingleton(realMetadata);
        services.AddSingleton<IDeviceFacade>(facade);
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        // Add other necessary services
        services.AddSingleton(new Mock<IScriptExecutionService>().Object);
        services.AddSingleton(facade.Notify);
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<AdvancedSimulationViewModel>();

        var provider = services.BuildServiceProvider();
        var factory = new ViewModelFactory(provider);

        var vm = new MainViewModel(
            factory,
            facade,
            realConfig,
            realMetadata,
            facade.Notify,
            provider.GetRequiredService<IScriptExecutionService>());

        // Verify: ViewModel is properly initialized
        vm.Deposit.ShouldNotBeNull();
        vm.Dispense.ShouldNotBeNull();
        vm.Inventory.ShouldNotBeNull();
        vm.PosTransaction.ShouldNotBeNull();

        // Verify: Total amount reflects mock inventory
        vm.Dispense.TotalAmount.Value.ShouldBe(10000m);

        // Verify: IsInDepositMode is false by default
        vm.Deposit.IsInDepositMode.Value.ShouldBeFalse();

        // Verify: DispenseAmountInput is empty by default
        vm.Dispense.DispenseAmountInput.Value.ShouldBe("");
    }
}
