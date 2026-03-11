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
using Microsoft.PointOfService;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>入金モードの ViewModel 動作をシミュレートして検証するテストクラス。</summary>
public class DepositModeViewModelTest
{
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly DepositController DepositController;
    private readonly DispenseController _dispenseController;
    private readonly MainViewModel _mainViewModel;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly DenominationKey _testKey = new(1000, CurrencyCashType.Bill);
    private readonly IDeviceFacade _facade;

    /// <summary>DepositModeViewModelTest の新しいインスタンスを初期化します。</summary>
    public DepositModeViewModelTest()
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
        hardwareManager.SetConnected(true);
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
    }

    /// <summary>DenominationViewModel の IsAcceptingCash プロパティが中断状態を正しく反映することを検証します。</summary>
    /// <remarks>
    /// 入金開始、中断、再開の各ステータスにおいて、IsAcceptingCash が期待通りに変化することを確認します。
    /// </remarks>
    [Fact]
    public void DenominationViewModelIsAcceptingCashShouldReflectPausedState()
    {
        // Arrange
        var config = new DenominationSettings();
        var monitor = new CashStatusMonitor(_mockInventory.Object, _testKey, config.NearEmpty, config.NearFull, config.Full);
        var configProvider = _mainViewModel.ConfigProvider;
        var denVm = new DenominationViewModel(_facade, _testKey, _metadataProvider, monitor, configProvider);
        DepositController.BeginDeposit();

        // Assert: Running
        denVm.IsAcceptingCash.CurrentValue.ShouldBeTrue();

        // Act: Pause
        DepositController.PauseDeposit(CashDepositPause.Pause);

        // Assert: Paused
        denVm.IsAcceptingCash.CurrentValue.ShouldBeFalse();

        // Act: Resume
        DepositController.PauseDeposit(CashDepositPause.Restart);

        // Assert: Running again
        denVm.IsAcceptingCash.CurrentValue.ShouldBeTrue();
    }

    /// <summary>MainViewModel の CurrentModeName が状態遷移を正しく反映することを検証します。</summary>
    /// <remarks>
    /// IDLE, COUNTING, PAUSED, FIXED の各状態遷移後の表示文字列を確認します。
    /// </remarks>
    [Fact]
    public void MainViewModelCurrentModeNameShouldReflectTransitions()
    {
        // Initial
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("IDLE");

        // Start
        DepositController.BeginDeposit();
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("COUNTING");

        // Pause
        _mainViewModel.Deposit.PauseDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("PAUSED");

        // Resume
        _mainViewModel.Deposit.ResumeDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("COUNTING");

        // Fix
        _mainViewModel.Deposit.FixDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("FIXED");

        // End
        _mainViewModel.Deposit.StoreDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("IDLE");
    }
}
