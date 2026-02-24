using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>ViewModel 全体の基本動作や初期状態を検証するテストクラス。</summary>
public class ViewModelTest
{
    /// <summary>MainViewModel の初期状態が正しくセットアップされることを検証する。</summary>
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
        realConfig.Config.CurrencyCode = "JPY";

        var realMetadata = new CurrencyMetadataProvider(realConfig);
        var realMonitors = new MonitorsProvider(mockInventory.Object, realConfig, realMetadata);
        var realAggregator = new OverallStatusAggregatorProvider(realMonitors);

        var mockManager = new Mock<CashChangerManager>(mockInventory.Object, mockHistory.Object, new ChangeCalculator());

        var realHardware = new HardwareStatusManager();
        var depositController = new DepositController(mockInventory.Object);
        var mockSimulator = new Mock<IDeviceSimulator>();
        var dispenseController = new DispenseController(mockManager.Object, null, mockSimulator.Object);

        var vm = new MainViewModel(
            mockInventory.Object,
            mockHistory.Object,
            mockManager.Object,
            realMonitors,
            realAggregator,
            realConfig,
            realMetadata,
            hardwareStatusManager: realHardware,
            depositController: depositController,
            dispenseController: dispenseController,
            cashChanger: new SimulatorCashChanger(realConfig, mockInventory.Object, mockHistory.Object, mockManager.Object, depositController, dispenseController, realAggregator, realHardware));

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
