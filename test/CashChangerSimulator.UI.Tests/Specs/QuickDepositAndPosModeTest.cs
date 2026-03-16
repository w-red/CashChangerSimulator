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
using Moq;
using R3;
using Shouldly;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>クイック入金と POS 取引モードの連動を検証するテストクラス。</summary>
public class QuickDepositAndPosModeTest : IClassFixture<PosTransactionViewModelFixture>
{
    private readonly PosTransactionViewModelFixture _fixture;
    private readonly MainViewModel _mainViewModel;

    public QuickDepositAndPosModeTest(PosTransactionViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        _mainViewModel = _fixture.CreateMainViewModel();
        _fixture.Hardware.SetConnected(true);
    }

    /// <summary>クイック入金コマンドが内訳を計算し、入金を完了させることを検証する。</summary>
    [Fact]
    public async Task QuickDepositCommandShouldCalculateBreakdownAndCompleteDeposit()
    {
        // Arrange
        var monitor = new CashStatusMonitor(_fixture.Inventory, new DenominationKey(1, CurrencyCashType.Bill), 5, 90, 100);

        var configProvider = _mainViewModel.ConfigProvider;

        _ = new List<DenominationViewModel>
        {
            new(_mainViewModel.Facade, new DenominationKey(10000, CurrencyCashType.Bill), _fixture.MetadataProvider, monitor, configProvider),
            new(_mainViewModel.Facade, new DenominationKey(5000, CurrencyCashType.Bill), _fixture.MetadataProvider, monitor, configProvider),
            new(_mainViewModel.Facade, new DenominationKey(1000, CurrencyCashType.Bill), _fixture.MetadataProvider, monitor, configProvider),
            new(_mainViewModel.Facade, new DenominationKey(500, CurrencyCashType.Coin), _fixture.MetadataProvider, monitor, configProvider),
            new(_mainViewModel.Facade, new DenominationKey(100, CurrencyCashType.Coin), _fixture.MetadataProvider, monitor, configProvider),
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
