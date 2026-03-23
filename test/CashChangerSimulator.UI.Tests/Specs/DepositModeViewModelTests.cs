using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>入金モードの ViewModel 動作をシミュレートして検証するテストクラス。</summary>
public class DepositModeViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;
    private readonly MainViewModel _mainViewModel;
    private readonly DenominationKey _testKey = new(1000, CurrencyCashType.Bill);

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public DepositModeViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        _mainViewModel = _fixture.CreateMainViewModel();
        _fixture.Hardware.SetConnected(true);
    }

    /// <summary>DenominationViewModel の IsAcceptingCash プロパティが中断状態を正しく反映することを検証します。</summary>
    [Fact]
    public void DenominationViewModelIsAcceptingCashShouldReflectPausedState()
    {
        // Arrange
        var config = new DenominationSettings();
        var monitor = new CashStatusMonitor(_fixture.Inventory, _testKey, config.NearEmpty, config.NearFull, config.Full);
        var denVm = new DenominationViewModel(_mainViewModel.Facade, _testKey, _fixture.MetadataProvider, monitor, _mainViewModel.ConfigProvider);
        _fixture.DepositController.BeginDeposit();

        // Assert: Running
        denVm.IsAcceptingCash.CurrentValue.ShouldBeTrue();

        // Act: Pause
        _fixture.DepositController.PauseDeposit(CashDepositPause.Pause);

        // Assert: Paused
        denVm.IsAcceptingCash.CurrentValue.ShouldBeFalse();

        // Act: Resume
        _fixture.DepositController.PauseDeposit(CashDepositPause.Restart);

        // Assert: Running again
        denVm.IsAcceptingCash.CurrentValue.ShouldBeTrue();
    }

    /// <summary>MainViewModel の CurrentModeName が状態遷移を正しく反映することを検証します。</summary>
    [Fact]
    public void MainViewModelCurrentModeNameShouldReflectTransitions()
    {
        // Initial
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusIdle", "IDLE"));

        // Start
        _fixture.DepositController.BeginDeposit();
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusCounting", "COUNTING"));

        // Pause
        _mainViewModel.Deposit.PauseDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusPaused", "PAUSED"));

        // Resume
        _mainViewModel.Deposit.ResumeDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusCounting", "COUNTING"));

        // Fix
        _mainViewModel.Deposit.FixDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusFixed", "FIXED"));

        // End
        _mainViewModel.Deposit.StoreDepositCommand.Execute(Unit.Default);
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain(ResourceHelper.GetAsString("StatusIdle", "IDLE"));
    }

    /// <summary>要求額入力が正しく反映され、連動することを検証します。</summary>
    [Fact]
    public void RequiredAmountInputShouldSyncWithRequiredAmount()
    {
        // Act: Set required amount from UI
        _mainViewModel.Deposit.RequiredAmountInput.Value = "5000";

        // Assert: Device RequiredAmount should be updated
        _fixture.DepositController.RequiredAmount.ShouldBe(5000m);
        _mainViewModel.Deposit.RequiredAmount.Value.ShouldBe(5000m);

        // Act: Change RequiredAmount from Device
        _fixture.DepositController.RequiredAmount = 10000m;

        _mainViewModel.Deposit.RequiredAmountInput.Value.ShouldBe("10000");
        _mainViewModel.Deposit.RemainingAmount.CurrentValue.ShouldBe(10000m);
    }
}
