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
using CashChangerSimulator.UI.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PointOfService;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>入金モードの ViewModel 動作をシミュレートして検証するテストクラス。</summary>
public class DepositModeViewModelTest : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;
    private readonly MainViewModel _mainViewModel;
    private readonly DenominationKey _testKey = new(1000, CurrencyCashType.Bill);

    public DepositModeViewModelTest(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        _mainViewModel = _fixture.CreateMainViewModel();
        _fixture.Hardware.SetConnected(true);
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
        var monitor = new CashStatusMonitor(_fixture.Inventory, _testKey, config.NearEmpty, config.NearFull, config.Full);
        var configProvider = _mainViewModel.ConfigProvider;
        var denVm = new DenominationViewModel(_fixture.CreateMainViewModel().Facade, _testKey, _fixture.MetadataProvider, monitor, configProvider);
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
    /// <remarks>
    /// IDLE, COUNTING, PAUSED, FIXED の各状態遷移後の表示文字列を確認します。
    /// </remarks>
    [Fact]
    public void MainViewModelCurrentModeNameShouldReflectTransitions()
    {
        // Initial
        _mainViewModel.Deposit.CurrentModeName.CurrentValue.ShouldContain("IDLE");

        // Start
        _fixture.DepositController.BeginDeposit();
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
