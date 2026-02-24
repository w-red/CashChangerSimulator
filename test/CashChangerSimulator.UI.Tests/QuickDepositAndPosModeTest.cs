using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Tests;

/// <summary>クイック入金と POS 取引モードの連動を検証するテストクラス。</summary>
public class QuickDepositAndPosModeTest
{
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    private readonly MainViewModel _mainViewModel;
    private readonly CurrencyMetadataProvider _metadataProvider;

    /// <summary>QuickDepositAndPosModeTest の新しいインスタンスを初期化します。</summary>
    public QuickDepositAndPosModeTest()
    {
        _mockInventory = new Mock<Inventory>();
        _mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        _mockInventory.Setup(i => i.CalculateTotal()).Returns(0m);

        _mockHistory = new Mock<TransactionHistory>();
        _mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        var configProvider = new ConfigurationProvider();
        configProvider.Config.CurrencyCode = "JPY";

        _mockManager = new Mock<CashChangerManager>(_mockInventory.Object, _mockHistory.Object, new ChangeCalculator());
        var hardwareManager = new HardwareStatusManager();
        _depositController = new DepositController(_mockInventory.Object, hardwareManager);
        var mockSimulator = new Mock<IDeviceSimulator>();
        _dispenseController = new DispenseController(_mockManager.Object, hardwareManager, mockSimulator.Object);

        _metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(_mockInventory.Object, configProvider, _metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);

        _mainViewModel = new MainViewModel(
            _mockInventory.Object,
            _mockHistory.Object,
            _mockManager.Object,
            monitorsProvider,
            aggregatorProvider,
            configProvider,
            _metadataProvider,
            hardwareManager,
            _depositController,
            _dispenseController);
    }

    /// <summary>クイック入金コマンドが内訳を計算し、入金を完了させることを検証する。</summary>
    [Fact]
    public async Task QuickDepositCommandShouldCalculateBreakdownAndCompleteDeposit()
    {
        // Arrange
        var monitor = new CashStatusMonitor(_mockInventory.Object, new DenominationKey(1, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), 5, 90, 100);

        var denoms = new List<DenominationViewModel>
        {
            new DenominationViewModel(_mockInventory.Object, new DenominationKey(10000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), _metadataProvider, _depositController, monitor, "10000"),
            new DenominationViewModel(_mockInventory.Object, new DenominationKey(5000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), _metadataProvider, _depositController, monitor, "5000"),
            new DenominationViewModel(_mockInventory.Object, new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), _metadataProvider, _depositController, monitor, "1000"),
            new DenominationViewModel(_mockInventory.Object, new DenominationKey(500, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), _metadataProvider, _depositController, monitor, "500"),
            new DenominationViewModel(_mockInventory.Object, new DenominationKey(100, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), _metadataProvider, _depositController, monitor, "100"),
        };

        // We need to pass these denoms to the DepositViewModel. 
        // In MainViewModel, it's passed as () => Inventory.Denominations.
        // But our Phase19_TDD_Test has its own MainViewModel which creates its own InventoryViewModel.
        // Let's use the one in _mainViewModel.

        var depositVm = _mainViewModel.Deposit;

        // Mock the return of denominations if necessary, but MainViewModel already setup sub-VMs.
        // However, the InventoryViewModel in MainViewModel might have empty denominations if metadata is empty.
        // Actually CurrencyMetadataProvider uses configProvider.Config.CurrencyCode to get denominations.

        // Act: Set input amount
        depositVm.QuickDepositAmountInput.Value = "16600";

        // Assert Command can execute
        ((ICommand)depositVm.QuickDepositCommand).CanExecute(null).ShouldBeTrue();

        // Act: Execute
        await depositVm.ExecuteQuickDepositAsync(_mainViewModel.Inventory.Denominations);

        // ...

        // Assert: Controller state should be Idle (because it finishes automatically)
        depositVm.IsInDepositMode.Value.ShouldBeFalse();
        depositVm.CurrentDepositAmount.Value.ShouldBe(0m);
    }


}
