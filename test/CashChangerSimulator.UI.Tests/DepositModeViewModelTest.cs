using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.PointOfService;
using Moq;
using R3;
using Xunit;

namespace CashChangerSimulator.UI.Tests;

public class DepositModeViewModelTest
{
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly DepositController _depositController;
    private readonly MainViewModel _mainViewModel;
    private readonly CashChangerSimulator.UI.Wpf.Services.CurrencyMetadataProvider _metadataProvider;
    private readonly DenominationKey _testKey = new(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill);

    public DepositModeViewModelTest()
    {
        _mockInventory = new Mock<Inventory>();
        _mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        _mockInventory.Setup(i => i.CalculateTotal()).Returns(0m);

        _mockHistory = new Mock<TransactionHistory>();
        _mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        var configProvider = new ConfigurationProvider();
        configProvider.Config.CurrencyCode = "JPY";

        _mockManager = new Mock<CashChangerManager>(_mockInventory.Object, _mockHistory.Object);
        var hardwareManager = new HardwareStatusManager();
        _depositController = new DepositController(_mockInventory.Object, _mockManager.Object, configProvider.Config.Simulation, hardwareManager);

        _metadataProvider = new CashChangerSimulator.UI.Wpf.Services.CurrencyMetadataProvider(configProvider);
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
            _depositController);
    }

    [Fact]
    public void DenominationViewModel_IsAcceptingCash_ShouldReflectPausedState()
    {
        // Arrange
        var config = new CashChangerSimulator.Core.Configuration.DenominationSettings();
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

    [Fact]
    public void MainViewModel_CurrentModeName_ShouldReflectTransitions()
    {
        // Initial
        Assert.Contains("IDLE", _mainViewModel.CurrentModeName.CurrentValue);

        // Start
        _depositController.BeginDeposit();
        Assert.Contains("COUNTING", _mainViewModel.CurrentModeName.CurrentValue);

        // Pause
        _mainViewModel.PauseDepositCommand.Execute(Unit.Default);
        Assert.Contains("PAUSED", _mainViewModel.CurrentModeName.CurrentValue);

        // Resume
        _mainViewModel.ResumeDepositCommand.Execute(Unit.Default);
        Assert.Contains("COUNTING", _mainViewModel.CurrentModeName.CurrentValue);

        // Fix
        _mainViewModel.FixDepositCommand.Execute(Unit.Default);
        Assert.Contains("FIXED", _mainViewModel.CurrentModeName.CurrentValue);

        // End
        _mainViewModel.StoreDepositCommand.Execute(Unit.Default);
        Assert.Contains("IDLE", _mainViewModel.CurrentModeName.CurrentValue);
    }
}

// Dummy implementation of MetaDataProvider if needed, but we used actual one above.
// Note: DenominationViewModel constructor was updated in MainViewModel.cs:229
// Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, _depositController, displayName));
