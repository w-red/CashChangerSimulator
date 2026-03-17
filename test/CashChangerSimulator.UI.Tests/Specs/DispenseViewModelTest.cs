using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Shouldly;
using Moq;
using Xunit;
using CashChangerSimulator.Core.Monitoring;
using R3;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DispenseViewModel の動作を検証するテストクラス。</summary>
public class DispenseViewModelTest : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public DispenseViewModelTest(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    /// <summary>初期状態のプロパティ値が正しいことを検証します。</summary>
    [Fact]
    public void InitialStateShouldHaveCorrectProperties()
    {
        // Act
        var vm = _fixture.CreateDispenseViewModel();

        // Assert
        vm.TotalAmount.Value.ShouldBe(0m);
        vm.IsBusy.CurrentValue.ShouldBeFalse();
        vm.Status.CurrentValue.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>出金金額入力のバリデーションロジックを検証します。</summary>
    /// <remarks>非数値、負数、残高不足、および正常値の各ケースを確認します。</remarks>
    [Fact]
    public void DispenseAmountInputValidationShouldWork()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        _fixture.Inventory.SetCount(denKey, 1);

        // Act & Assert (Invalid number)
        vm.DispenseAmountInput.Value = "abc";
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();

        // Act & Assert (Negative)
        vm.DispenseAmountInput.Value = "-100";
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();

        // Act & Assert (Insufficient funds)
        vm.DispenseAmountInput.Value = (denKey.Value + 1).ToString();
        vm.DispenseAmountInput.HasErrors.ShouldBeTrue();

        // Act & Assert (Valid)
        vm.DispenseAmountInput.Value = denKey.Value.ToString();
        vm.DispenseAmountInput.HasErrors.ShouldBeFalse();
    }

    /// <summary>出金コマンドが適切な状態でのみ実行可能であることを検証します。</summary>
    [Fact]
    public void DispenseCommandCanExecuteShouldDependOnState()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        _fixture.Inventory.SetCount(denKey, 10);
        vm.DispenseAmountInput.Value = denKey.Value.ToString();

        // Assert (Normal)
        vm.DispenseCommand.CanExecute().ShouldBeTrue();

        // Assert (Jammed)
        _fixture.Hardware.SetJammed(true);
        vm.DispenseCommand.CanExecute().ShouldBeFalse();
        _fixture.Hardware.SetJammed(false);

        // Assert (Deposit Mode)
        var isInDepositMode = new BindableReactiveProperty<bool>(true);
        var vmInDeposit = _fixture.CreateDispenseViewModel(isInDepositMode: isInDepositMode);
        vmInDeposit.DispenseAmountInput.Value = denKey.Value.ToString();
        vmInDeposit.DispenseCommand.CanExecute().ShouldBeFalse();
    }

    /// <summary>クイック出金コマンドが正しく 1 枚の出金を要求することを検証します。</summary>
    [Fact]
    public void QuickDispenseCommandShouldRequestOneDenomination()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        // Act
        var facade = _fixture.CreateFacade();
        var monitor = _fixture.Monitors.Monitors.First(m => m.Key.Equals(denKey));
        var denVm = new DenominationViewModel(facade, denKey, _fixture.MetadataProvider, monitor, _fixture.ConfigProvider);
        vm.QuickDispenseCommand.Execute(denVm);
        
        // Wait for async operation (ExecuteDispense in Controller runs in Task.Run)
        System.Threading.Thread.Sleep(200);

        // Assert
        _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")).ShouldBeTrue();
    }

    /// <summary>一括出金コマンドが指定された枚数での出金を要求することを検証します。</summary>
    [Fact]
    public void DispenseBulkCommandShouldRequestMultipleDenominations()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        var counts = new Dictionary<DenominationKey, int> { [denKey] = 2 };

        // Act
        vm.DispenseBulkCommand.Execute(counts);
        
        // Wait for async operation
        System.Threading.Thread.Sleep(200);

        // Assert
        _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")).ShouldBeTrue();
    }

    /// <summary>出金処理中に例外が発生した場合のエラーハンドリングを検証します。</summary>
    [Fact]
    public void DispenseCashShouldHandleExceptionAndSetError()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        _fixture.Inventory.SetCount(denKey, 10);
        vm.DispenseAmountInput.Value = denKey.Value.ToString();
        _fixture.CashChanger.SimulateDispenseException = true;

        // Act
        vm.DispenseCommand.Execute(Unit.Default);
        
        // Wait for async operation
        System.Threading.Thread.Sleep(200);

        // Assert
        System.Threading.Thread.Sleep(100); // Wait for async operation to complete
        vm.IsDeviceError.CurrentValue.ShouldBeTrue();
    }

    /// <summary>シミュレーションコマンドがハードウェアの状態を正しく変化させることを検証します。</summary>
    [Fact]
    public void SimulationCommandsShouldUpdateHardwareState()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();

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
        var vm = _fixture.CreateDispenseViewModel();
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
        var isInDepositMode = new BindableReactiveProperty<bool>(true);
        var vm = _fixture.CreateDispenseViewModel(isInDepositMode);
        vm.DispenseAmountInput.Value = "1000";

        // Act
        vm.DispenseCommand.Execute(Unit.Default);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>空の入力（カウントが0）でバルク払出を実行した場合に何もしないことを検証します。</summary>
    [Fact]
    public void DispenseBulkCommandShouldDoNothingWhenCountsIsEmpty()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();
        var counts = new Dictionary<DenominationKey, int>();

        // Act
        vm.DispenseBulkCommand.Execute(counts);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>null の入力でバルク払出を実行した場合に何もしないことを検証します。</summary>
    [Fact]
    public void DispenseBulkCommandShouldDoNothingWhenCountsIsNull()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();

        // Act
        vm.DispenseBulkCommand.Execute(null!);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>入金モード中にクイック払出を実行しようとした場合に警告が表示されることを検証します。</summary>
    [Fact]
    public void QuickDispenseCommandShouldShowWarningInDepositMode()
    {
        // Assemble
        var isInDepositMode = new BindableReactiveProperty<bool>(true);
        var vm = _fixture.CreateDispenseViewModel(isInDepositMode);
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        var facade = _fixture.CreateFacade();
        var monitor = _fixture.Monitors.Monitors.First(m => m.Key.Equals(denKey));
        var denVm = new DenominationViewModel(facade, denKey, _fixture.MetadataProvider, monitor, _fixture.ConfigProvider);

        // Act
        vm.QuickDispenseCommand.Execute(denVm);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>null の金種でクイック払出を実行した場合に何もしないことを検証します。</summary>
    [Fact]
    public void QuickDispenseCommandShouldDoNothingWhenDenominationIsNull()
    {
        // Assemble
        var vm = _fixture.CreateDispenseViewModel();

        // Act
        vm.QuickDispenseCommand.Execute(null!);

        // Assert
        _fixture.NotifyServiceMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _fixture.CashChanger.OposHistory.Any(h => h.Contains("DispenseCash")).ShouldBeFalse();
    }
}
