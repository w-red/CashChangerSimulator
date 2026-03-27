using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.UI.Wpf.Services;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DispenseViewModel の動作を検証するテストクラス。</summary>
public class DispenseViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public DispenseViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        
        // Setup initial inventory
        _fixture.SetInventory(
            (TestConstants.Key100, 100),
            (TestConstants.Key1000, 100)
        );
    }


    private DispenseViewModel CreateViewModel(BindableReactiveProperty<bool>? isInDepositMode = null)
    {
        return _fixture.CreateDispenseViewModel(isInDepositMode);
    }

    /// <summary>初期状態のプロパティ値が正しいことを検証します。</summary>
    [Fact]
    public void InitialStateShouldHaveCorrectProperties()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.TotalAmount.Value.ShouldBe(100 * 100 + 1000 * 100);
        vm.IsBusy.CurrentValue.ShouldBeFalse();
        vm.Status.CurrentValue.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>出金金額入力のバリデーションロジックを検証します。</summary>
    [Fact]
    public void DispenseAmountInputValidationShouldWork()
    {
        // Assemble
        var vm = CreateViewModel();

        // Act & Assert (Empty)
        vm.DispenseAmountInput.Value = "";
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        // Act & Assert (Invalid number)
        vm.DispenseAmountInput.Value = "abc";
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        // Act & Assert (Negative)
        vm.DispenseAmountInput.Value = "-100";
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        // Act & Assert (Insufficient funds)
        var tooMuch = (100 * 100 + 1000 * 100) + 1;
        vm.DispenseAmountInput.Value = tooMuch.ToString();
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        // Act & Assert (Valid)
        vm.DispenseAmountInput.Value = "100";
        vm.DispenseAmountInput.HasErrors.ShouldBeFalse();
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeTrue();
    }

    /// <summary>出金コマンドが適切な状態でのみ実行可能であることを検証します。</summary>
    [Fact]
    public void DispenseCommandCanExecuteShouldDependOnState()
    {
        // Assemble
        var vm = CreateViewModel();
        vm.DispenseAmountInput.Value = "100";

        // Assert (Normal)
        vm.DispenseCommand.CanExecute().ShouldBeTrue();

        // Assert (Jammed)
        _fixture.Hardware.SetJammed(true);
        vm.DispenseCommand.CanExecute().ShouldBeFalse();
        _fixture.Hardware.SetJammed(false);

        // Assert (Deposit Mode)
        var isInDepositMode = new BindableReactiveProperty<bool>(true);
        var vmInDeposit = CreateViewModel(isInDepositMode: isInDepositMode);
        vmInDeposit.DispenseAmountInput.Value = "100";
        vmInDeposit.DispenseCommand.CanExecute().ShouldBeFalse();
    }

    /// <summary>クイック出金コマンドが正しく出金を要求することを検証します。</summary>
    [Fact]
    public void QuickDispenseCommandShouldExecuteDispense()
    {
        // Arrange
        var vm = CreateViewModel();
        var factory = _fixture.ServiceProvider.GetRequiredService<IViewModelFactory>();
        var denominationVm = factory.CreateDenominationViewModel(TestConstants.Key100);

        // Act
        vm.QuickDispenseCommand.Execute(denominationVm);

        // Assert
        vm.DispensingAmount.Value.ShouldBe(100);
        
        // Wait for async operation
        FlaUI.Core.Tools.Retry.WhileFalse(() => _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")), TimeSpan.FromSeconds(2)).Success.ShouldBeTrue();
    }

    /// <summary>一括出金コマンドが指定された枚数での出金を要求することを検証します。</summary>
    [Fact]
    public void DispenseBulkCommandShouldRequestMultipleDenominations()
    {
        // Assemble
        var vm = CreateViewModel();
        var counts = new Dictionary<DenominationKey, int> { [TestConstants.Key100] = 2 };

        // Act
        vm.DispenseBulkCommand.Execute(counts);
        
        // Assert
        vm.DispensingAmount.Value.ShouldBe(200);
        
        // Wait for async operation
        FlaUI.Core.Tools.Retry.WhileFalse(() => _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")), TimeSpan.FromSeconds(2)).Success.ShouldBeTrue();
    }

    /// <summary>出金処理中に例外が発生した場合のエラーハンドリングを検証します。</summary>
    [Fact]
    public void DispenseCashShouldHandleExceptionAndSetError()
    {
        // Assemble
        var vm = CreateViewModel();
        vm.DispenseAmountInput.Value = "100";
        _fixture.CashChanger.SimulateDispenseException = true;

        // Act
        vm.DispenseCommand.Execute(Unit.Default);
        
        // Assert
        FlaUI.Core.Tools.Retry.WhileFalse(() => vm.IsDeviceError.CurrentValue, TimeSpan.FromSeconds(2)).Success.ShouldBeTrue();
    }

    /// <summary>シミュレーションコマンドがハードウェアの状態を正しく変化させることを検証します。</summary>
    [Fact]
    public void SimulationCommandsShouldUpdateHardwareState()
    {
        // Assemble
        var vm = CreateViewModel();

        // Act & Assert (Jam)
        vm.SimulateJamCommand.Execute(Unit.Default);
        vm.IsJammed.CurrentValue.ShouldBeTrue();

        // Act & Assert (Overlap)
        vm.SimulateOverlapCommand.Execute(Unit.Default);
        vm.IsOverlapped.CurrentValue.ShouldBeTrue();
    }

    /// <summary>エラーリセットコマンドが状態をクリアすることを検証します。</summary>
    [Fact]
    public void ResetErrorCommandShouldClearStatus()
    {
        // Assemble
        var vm = CreateViewModel();
        _fixture.Hardware.SetJammed(true);

        // Act
        vm.ResetErrorCommand.Execute(Unit.Default);

        // Assert
        vm.IsJammed.CurrentValue.ShouldBeFalse();
    }

    /// <summary>入金モード中に払出を実行しようとした場合に警告が表示されることを検証します。</summary>
    [Fact]
    public void DispenseCommandShouldShowWarningInDepositMode()
    {
        // Assemble
        var vm = CreateViewModel();
        vm.DispenseAmountInput.Value = "1000";
        
        // Trigger the service's check for IsDepositInProgress
        _fixture.DepositController.BeginDeposit();

        // Act
        vm.DispenseCommand.Execute(Unit.Default);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>空の入力でバルク払出を実行した場合に何もしないことを検証します。</summary>
    [Fact]
    public void DispenseBulkCommandShouldDoNothingWhenCountsIsEmpty()
    {
        // Assemble
        var vm = CreateViewModel();
        var counts = new Dictionary<DenominationKey, int>();

        // Act
        vm.DispenseBulkCommand.Execute(counts);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>null の金種でクイック払出を実行した場合に何もしないことを検証します。</summary>
    [Fact]
    public void QuickDispenseCommandShouldDoNothingWhenDenominationIsNull()
    {
        // Assemble
        var vm = CreateViewModel();

        // Act
        vm.QuickDispenseCommand.Execute(null!);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")).ShouldBeFalse();
    }
}
