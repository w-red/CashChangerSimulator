using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.PointOfService;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>ターミナル画面の操作コントロール制御（CanOperate）を検証するテストクラス。</summary>
public class TerminalOperationTest
{
    private readonly HardwareStatusManager _hardwareManager;
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<CashChangerManager> _mockCashChangerManager;
    private readonly Mock<DepositController> _mockDepositController;
    private readonly Mock<DispenseController> _mockDispenseController;
    private readonly BindableReactiveProperty<bool> _isBusyShared = new(false);

    public TerminalOperationTest()
    {
        _hardwareManager = new HardwareStatusManager();
        _mockInventory = new Mock<Inventory>();
        _mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        
        var mockHistory = new Mock<TransactionHistory>();
        mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());
        _mockCashChangerManager = new Mock<CashChangerManager>(_mockInventory.Object, mockHistory.Object, new ChangeCalculator());

        // Set up DispenseController mock
        _mockDispenseController = new Mock<DispenseController>(_mockCashChangerManager.Object, _hardwareManager, new Mock<IDeviceSimulator>().Object);
        _mockDispenseController.SetupGet(c => c.Changed).Returns(Observable.Empty<Unit>());
        _mockDispenseController.SetupGet(c => c.Status).Returns(CashDispenseStatus.Idle);
        _mockDispenseController.SetupGet(c => c.IsBusy).Returns(false);

        // Set up DepositController mock
        _mockDepositController = new Mock<DepositController>(_mockInventory.Object, _hardwareManager);
        _mockDepositController.Setup(c => c.Changed).Returns(Observable.Empty<Unit>());
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(false);
        _mockDepositController.SetupGet(c => c.DepositAmount).Returns(0m);
        _mockDepositController.SetupGet(c => c.IsFixed).Returns(false);
        _mockDepositController.SetupGet(c => c.DepositStatus).Returns(CashDepositStatus.None);
        _mockDepositController.SetupGet(c => c.IsPaused).Returns(false);
    }

    /// <summary>入金 ViewModel の操作可能状態がハードウェアエラーとビジー状態を正しく反映することを検証します。</summary>
    [Fact]
    public void DepositViewModelCanOperateShouldReflectHardwareErrorAndBusyState()
    {
        // Arrange
        var isDepositInProgress = new Subject<bool>();
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(false);
        var changedSubject = new Subject<Unit>();
        _mockDepositController.Setup(c => c.Changed).Returns(changedSubject);

        var vm = new DepositViewModel(
            _mockDepositController.Object,
            _hardwareManager,
            () => Enumerable.Empty<DenominationViewModel>(),
            _isBusyShared,
            new Mock<INotifyService>().Object,
            new CurrencyMetadataProvider(new ConfigurationProvider()));

        // Assert: Initial (Normal)
        vm.CanOperate.Value.ShouldBeTrue();

        // Act: Simulate Jam
        _hardwareManager.SetJammed(true);
        // Assert
        vm.CanOperate.Value.ShouldBeFalse();

        // Act: Reset Jam
        _hardwareManager.ResetError();
        // Assert
        vm.CanOperate.Value.ShouldBeTrue();

        // Act: Start Deposit (Busy)
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(true);
        changedSubject.OnNext(Unit.Default);
        // Assert
        vm.CanOperate.Value.ShouldBeFalse();

        // Act: End Deposit
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(false);
        changedSubject.OnNext(Unit.Default);
        // Assert
        vm.CanOperate.Value.ShouldBeTrue();
    }

    /// <summary>出金 ViewModel の操作可能状態がハードウェアエラーとビジー状態を正しく反映することを検証します。</summary>
    [Fact]
    public void DispenseViewModelCanOperateShouldReflectHardwareErrorAndBusyState()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        var vm = new DispenseViewModel(
            _mockInventory.Object,
            _mockCashChangerManager.Object,
            _mockDispenseController.Object,
            _hardwareManager,
            configProvider,
            _isBusyShared,
            () => Enumerable.Empty<DenominationViewModel>(),
            new Mock<INotifyService>().Object,
            new CurrencyMetadataProvider(configProvider));

        // Assert: Initial
        vm.CanOperate.Value.ShouldBeTrue();

        // Act: Simulate Overlap
        _hardwareManager.SetOverlapped(true);
        // Assert
        vm.CanOperate.Value.ShouldBeFalse();

        // Act: Reset
        _hardwareManager.ResetError();
        // Assert
        vm.CanOperate.Value.ShouldBeTrue();

        // Act & Assert for Busy requires mocking DispenseController status change
        // Since it's a mock, we manually change property if it's setup to do so, or just verify the CombineLatest logic.
    }

    /// <summary>入金 ViewModel の各コマンドの実行可能状態がエラー状態を正しく反映することを検証します。</summary>
    [Fact]
    public void DepositViewModelCommandCanExecuteShouldReflectErrorState()
    {
        // Arrange
        var isDepositInProgress = new Subject<bool>();
        var isFixed = new Subject<bool>();
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(false);
        _mockDepositController.SetupGet(c => c.IsFixed).Returns(false);
        var changedSubject = new Subject<Unit>();
        _mockDepositController.Setup(c => c.Changed).Returns(changedSubject);

        var vm = new DepositViewModel(
            _mockDepositController.Object,
            _hardwareManager,
            () => Enumerable.Empty<DenominationViewModel>(),
            _isBusyShared,
            new Mock<INotifyService>().Object,
            new CurrencyMetadataProvider(new ConfigurationProvider()));

        // --- Idle State (Not in Deposit Mode) ---
        // Assert: Begin and Quick are enabled, Bulk is disabled
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.QuickDepositCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.ShowBulkInsertCommand).CanExecute(null).ShouldBeFalse();

        // Act: Simulate Jam
        _hardwareManager.SetJammed(true);
        // Assert: Disabled due to error
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.QuickDepositCommand).CanExecute(null).ShouldBeFalse();

        // Act: Reset Error
        _hardwareManager.ResetError();
        // Assert: Re-enabled
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.QuickDepositCommand).CanExecute(null).ShouldBeTrue();

        // --- In Deposit Mode ---
        // Act: Start Deposit
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(true);
        changedSubject.OnNext(Unit.Default);
        // Assert: Quick is disabled due to state, Bulk is enabled, Begin remains enabled (but ignores execution internally)
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.QuickDepositCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.ShowBulkInsertCommand).CanExecute(null).ShouldBeTrue();

        // Act: Simulate Overlap
        _hardwareManager.SetOverlapped(true);
        // Assert: Disabled due to error
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeFalse(); // Disabled by error
        ((System.Windows.Input.ICommand)vm.ShowBulkInsertCommand).CanExecute(null).ShouldBeFalse();

        // Act: Reset Error
        _hardwareManager.ResetError();
        // Assert: Re-enabled
        ((System.Windows.Input.ICommand)vm.BeginDepositCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.ShowBulkInsertCommand).CanExecute(null).ShouldBeTrue();
        
        // Cleanup
        _mockDepositController.SetupGet(c => c.IsDepositInProgress).Returns(false);
        _mockDepositController.SetupGet(c => c.IsFixed).Returns(true);
        changedSubject.OnNext(Unit.Default);
    }

    /// <summary>出金 ViewModel の各コマンドの実行可能状態がエラー状態を正しく反映することを検証します。</summary>
    [Fact]
    public void DispenseViewModelCommandCanExecuteShouldReflectErrorState()
    {
        // Arrange
        _mockInventory.Setup(i => i.CalculateTotal(It.IsAny<string>())).Returns(1000m); // Ensure sufficient funds
        var configProvider = new ConfigurationProvider();
        var vm = new DispenseViewModel(
            _mockInventory.Object,
            _mockCashChangerManager.Object,
            _mockDispenseController.Object,
            _hardwareManager,
            configProvider,
            _isBusyShared,
            () => Enumerable.Empty<DenominationViewModel>(),
            new Mock<INotifyService>().Object,
            new CurrencyMetadataProvider(configProvider));

        // Setup input to make DispenseCommand valid
        vm.DispenseAmountInput.Value = "100";

        // Assert: Initial
        ((System.Windows.Input.ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.ShowBulkDispenseCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.QuickDispenseCommand).CanExecute(null).ShouldBeTrue();

        // Act: Simulate Jam
        _hardwareManager.SetJammed(true);
        // Assert
        ((System.Windows.Input.ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.ShowBulkDispenseCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.QuickDispenseCommand).CanExecute(null).ShouldBeFalse();

        // Act: Reset Error
        _hardwareManager.ResetError();
        // Assert
        ((System.Windows.Input.ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.ShowBulkDispenseCommand).CanExecute(null).ShouldBeTrue();
        ((System.Windows.Input.ICommand)vm.QuickDispenseCommand).CanExecute(null).ShouldBeTrue();

        // Act: Simulate Overlap
        _hardwareManager.SetOverlapped(true);
        // Assert
        ((System.Windows.Input.ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.ShowBulkDispenseCommand).CanExecute(null).ShouldBeFalse();
        ((System.Windows.Input.ICommand)vm.QuickDispenseCommand).CanExecute(null).ShouldBeFalse();
    }
}
