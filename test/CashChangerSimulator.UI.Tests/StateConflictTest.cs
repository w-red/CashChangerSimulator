using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Transactions;
using Moq;
using R3;
using Shouldly;
using System;

namespace CashChangerSimulator.UI.Tests;

/// <summary>状態競合時の警告ダイアログ動作を検証するテストクラス。</summary>
public class StateConflictTest
{
    private readonly Mock<INotifyService> _mockNotify;
    private readonly Mock<Inventory> _mockInventory;
    private readonly DepositController _depositController;
    private readonly Mock<DispenseController> _mockDispenseController;
    private readonly MainViewModel _mainViewModel;
    private readonly Subject<Unit> _dispenseChanged = new();

    public StateConflictTest()
    {
        _mockNotify = new Mock<INotifyService>();
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var configProvider = new ConfigurationProvider();
        configProvider.Config.CurrencyCode = "JPY";
        var invSettings = new InventorySettings();
        invSettings.Denominations.Add("B1000", new DenominationSettings { DisplayName = "1000 JPY" });
        configProvider.Config.Inventory.TryAdd("JPY", invSettings);
        
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadataProvider);
        
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var hardwareManager = new HardwareStatusManager();
        
        _depositController = new DepositController(inventory, hardwareManager);
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        
        _mockDispenseController = new Mock<DispenseController>(manager, hardwareManager, new Mock<IDeviceSimulator>().Object);
        _mockDispenseController.SetupGet(c => c.Changed).Returns(_dispenseChanged);

        var cashChangerMock = new Mock<SimulatorCashChanger>(configProvider, inventory, history, manager, _depositController, _mockDispenseController.Object, aggregatorProvider, hardwareManager);

        _mainViewModel = new MainViewModel(
            inventory: inventory,
            history: history,
            manager: manager,
            monitorsProvider: monitorsProvider,
            aggregatorProvider: aggregatorProvider,
            configProvider: configProvider,
            metadataProvider: metadataProvider,
            hardwareStatusManager: hardwareManager,
            depositController: _depositController,
            dispenseController: _mockDispenseController.Object,
            cashChanger: cashChangerMock.Object,
            notifyService: _mockNotify.Object);
    }

    [Fact]
    public void DispenseShouldShowWarningDuringDeposit()
    {
        // Arrange: Start deposit
        _depositController.BeginDeposit();
        _mainViewModel.Deposit.IsInDepositMode.Value.ShouldBeTrue();

        // Act: Attempt to dispense
        _mainViewModel.Dispense.DispenseAmountInput.Value = "1000";
        _mainViewModel.Dispense.DispenseCommand.Execute(Unit.Default);

        // Assert: Warning should be shown
        _mockNotify.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void DepositShouldShowWarningDuringDispense()
    {
        // Arrange: Simulate dispense busy
        _mockDispenseController.SetupGet(c => c.Status).Returns(CashDispenseStatus.Busy);
        _dispenseChanged.OnNext(Unit.Default);
        
        _mainViewModel.Dispense.IsBusy.Value.ShouldBeTrue();

        // Act: Attempt to begin deposit
        _mainViewModel.Deposit.BeginDepositCommand.Execute(Unit.Default);

        // Assert: Warning should be shown
        _mockNotify.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
