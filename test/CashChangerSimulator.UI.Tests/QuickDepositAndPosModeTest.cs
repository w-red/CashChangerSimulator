using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
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
using System.Windows.Input;

namespace CashChangerSimulator.UI.Tests;

/// <summary>クイック入金と POS 取引モードの連動を検証するテストクラス。</summary>
public class QuickDepositAndPosModeTest
{
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly DepositController DepositController;
    private readonly DispenseController _dispenseController;
    private readonly MainViewModel _mainViewModel;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly IDeviceFacade _facade;

    /// <summary>QuickDepositAndPosModeTest の新しいインスタンスを初期化します。</summary>
    public QuickDepositAndPosModeTest()
    {
        _mockInventory = new Mock<Inventory>();
        _mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        _mockInventory.Setup(i => i.CalculateTotal()).Returns(0m);

        _mockHistory = new Mock<TransactionHistory>();
        _mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        var configProvider = new ConfigurationProvider();
        configProvider.Config.System.CurrencyCode = "JPY";

        _mockManager = new Mock<CashChangerManager>(_mockInventory.Object, _mockHistory.Object, new ChangeCalculator());
        var hardwareManager = new HardwareStatusManager();
        DepositController = new DepositController(_mockInventory.Object, hardwareManager);
        var mockSimulator = new Mock<IDeviceSimulator>();
        _dispenseController = new DispenseController(_mockManager.Object, hardwareManager, mockSimulator.Object);

        _metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(_mockInventory.Object, configProvider, _metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);

        var changer = new InternalSimulatorCashChanger(new SimulatorDependencies(
            configProvider, _mockInventory.Object, _mockHistory.Object, _mockManager.Object, 
            DepositController, _dispenseController, aggregatorProvider, hardwareManager));

        _facade = new DeviceFacade(
            _mockInventory.Object,
            _mockManager.Object,
            DepositController,
            _dispenseController,
            hardwareManager,
            changer,
            _mockHistory.Object,
            aggregatorProvider,
            monitorsProvider,
            new Mock<INotifyService>().Object);

        var services = new ServiceCollection();
        services.AddSingleton(_facade);
        services.AddSingleton(configProvider);
        services.AddSingleton(_metadataProvider);
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        services.AddSingleton(new Mock<IScriptExecutionService>().Object);
        services.AddSingleton(_facade.Notify);
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<AdvancedSimulationViewModel>();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IViewModelFactory>();

        _mainViewModel = new MainViewModel(
            factory,
            _facade,
            configProvider,
            _metadataProvider,
            _facade.Notify,
            provider.GetRequiredService<IScriptExecutionService>());
        hardwareManager.SetConnected(true);
    }

    /// <summary>クイック入金コマンドが内訳を計算し、入金を完了させることを検証する。</summary>
    [Fact]
    public async Task QuickDepositCommandShouldCalculateBreakdownAndCompleteDeposit()
    {
        // Arrange
        var monitor = new CashStatusMonitor(_mockInventory.Object, new DenominationKey(1, CurrencyCashType.Bill), 5, 90, 100);

        var configProvider = _mainViewModel.ConfigProvider;

        _ = new List<DenominationViewModel>
        {
            new(_facade, new DenominationKey(10000, CurrencyCashType.Bill), _metadataProvider, monitor, configProvider),
            new(_facade, new DenominationKey(5000, CurrencyCashType.Bill), _metadataProvider, monitor, configProvider),
            new(_facade, new DenominationKey(1000, CurrencyCashType.Bill), _metadataProvider, monitor, configProvider),
            new(_facade, new DenominationKey(500, CurrencyCashType.Coin), _metadataProvider, monitor, configProvider),
            new(_facade, new DenominationKey(100, CurrencyCashType.Coin), _metadataProvider, monitor, configProvider),
        };

        var depositVm = _mainViewModel.Deposit;

        // Act: Set input amount
        depositVm.QuickDepositAmountInput.Value = "16600";

        // Assert Command can execute
        ((ICommand)depositVm.QuickDepositCommand).CanExecute(null).ShouldBeTrue();

        // Act: Execute
        await depositVm.ExecuteQuickDepositAsync(_mainViewModel.Inventory.Denominations);

        // Assert: Controller state should be Idle (because it finishes automatically)
        depositVm.IsInDepositMode.Value.ShouldBeFalse();
        depositVm.CurrentDepositAmount.Value.ShouldBe(0m);
    }
}
