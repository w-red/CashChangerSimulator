using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.PointOfService;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests;

/// <summary>入金モードの ViewModel 動作をシミュレートして検証するテストクラス。</summary>
public class DepositModeViewModelTest
{
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    private readonly MainViewModel _mainViewModel;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly DenominationKey _testKey = new(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill);

    /// <summary>DepositModeViewModelTest の新しいインスタンスを初期化します。</summary>
    public DepositModeViewModelTest()
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
            inventory: _mockInventory.Object,
            history: _mockHistory.Object,
            manager: _mockManager.Object,
            monitorsProvider: monitorsProvider,
            aggregatorProvider: aggregatorProvider,
            configProvider: configProvider,
            metadataProvider: _metadataProvider,
            hardwareStatusManager: hardwareManager,
            depositController: _depositController,
            dispenseController: _dispenseController,
            cashChanger: new SimulatorCashChanger(configProvider, _mockInventory.Object, _mockHistory.Object, _mockManager.Object, _depositController, _dispenseController, aggregatorProvider, hardwareManager),
            notifyService: new Mock<INotifyService>().Object);
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
        var denVm = new DenominationViewModel(_mockInventory.Object, _testKey, _metadataProvider, _depositController, monitor, "1000");
        _depositController.BeginDeposit();

        // Assert: Running
        Assert.True(denVm.IsAcceptingCash.CurrentValue);

        // Act: Pause
        _depositController.PauseDeposit(CashDepositPause.Pause);

        // Assert: Paused
        Assert.False(denVm.IsAcceptingCash.CurrentValue);

        // Act: Resume
        _depositController.PauseDeposit(CashDepositPause.Restart);

        // Assert: Running again
        Assert.True(denVm.IsAcceptingCash.CurrentValue);
    }

    /// <summary>MainViewModel の CurrentModeName が状態遷移を正しく反映することを検証します。</summary>
    /// <remarks>
    /// IDLE, COUNTING, PAUSED, FIXED の各状態遷移後の表示文字列を確認します。
    /// </remarks>
    [Fact]
    public void MainViewModelCurrentModeNameShouldReflectTransitions()
    {
        // Initial
        Assert.Contains("IDLE", _mainViewModel.Deposit.CurrentModeName.CurrentValue);

        // Start
        _depositController.BeginDeposit();
        Assert.Contains("COUNTING", _mainViewModel.Deposit.CurrentModeName.CurrentValue);

        // Pause
        _mainViewModel.Deposit.PauseDepositCommand.Execute(Unit.Default);
        Assert.Contains("PAUSED", _mainViewModel.Deposit.CurrentModeName.CurrentValue);

        // Resume
        _mainViewModel.Deposit.ResumeDepositCommand.Execute(Unit.Default);
        Assert.Contains("COUNTING", _mainViewModel.Deposit.CurrentModeName.CurrentValue);

        // Fix
        _mainViewModel.Deposit.FixDepositCommand.Execute(Unit.Default);
        Assert.Contains("FIXED", _mainViewModel.Deposit.CurrentModeName.CurrentValue);

        // End
        _mainViewModel.Deposit.StoreDepositCommand.Execute(Unit.Default);
        Assert.Contains("IDLE", _mainViewModel.Deposit.CurrentModeName.CurrentValue);
    }
}
