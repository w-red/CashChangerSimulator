using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Tests;

public class PosTransactionViewModelTest : IDisposable
{
    private readonly PosTransactionViewModelFixture _fixture = new();

    public PosTransactionViewModelTest()
    {
        _fixture.Initialize(PosTransactionTestConstants.TestCurrencyCode);
    }

    /// <summary>
    /// 取引開始時のOPOSシーケンス呼び出しを検証するテスト。
    /// 
    /// テストフロー:
    /// 1. 取引目標金額をセット
    /// 2. 取引開始コマンドを実行
    /// 
    /// 期待値: 初期化および入金開始シーケンス（Open, Claim, BeginDeposit）が正しく呼ばれること。
    /// </summary>
    [Fact]
    public void StartTransactionShouldCallOposSequence()
    {
        // Arrange
        var depVm = new Mock<DepositViewModel>(_fixture.DepositController, _fixture.Hardware, (Func<IEnumerable<DenominationViewModel>>)(() => Enumerable.Empty<DenominationViewModel>()));
        var dispVm = new Mock<DispenseViewModel>(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), (Func<IEnumerable<DenominationViewModel>>)(() => Enumerable.Empty<DenominationViewModel>()));
        
        var vm = new PosTransactionViewModel(depVm.Object, dispVm.Object, _fixture.CashChanger);

        vm.TargetAmountInput.Value = PosTransactionTestConstants.TargetAmount;

        // Act
        vm.StartCommand.Execute(Unit.Default);

        // Verify
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);
        vm.OposLog.ShouldContain(s => s.Contains("Open()"));
        vm.OposLog.ShouldContain(s => s.Contains("Claim(1000)"));
        vm.OposLog.ShouldContain(s => s.Contains("BeginDeposit()"));
    }

    /// <summary>
    /// 取引完了時のOPOSシーケンス呼び出しを検証するテスト。
    /// 
    /// テストフロー:
    /// 1. 取引を開始状態にする
    /// 2. 支払い金額に達するまでの現金を投入する
    /// 3. 取引完了を待機し、お釣りの支払いを実行
    /// 
    /// 期待値: 預り金確定、釣銭支払い、デバイス解放（EndDeposit, DispenseChange, Release, Close）が正しく呼ばれること。
    /// </summary>
    [Fact]
    public async Task CompleteTransactionShouldCallOposSequence()
    {
        // Arrange
        var depVm = new DepositViewModel(_fixture.DepositController, _fixture.Hardware, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        
        var vm = new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger);

        // Act
        await ExecuteCompleteTransaction(vm);

        // Verify
        VerifyCompletionSequence(vm);
    }

    private async Task ExecuteCompleteTransaction(PosTransactionViewModel vm)
    {
        vm.TargetAmountInput.Value = PosTransactionTestConstants.TargetAmount;
        // Simulate start
        vm.StartCommand.Execute(Unit.Default);
        vm.OposLog.Clear(); // Clear start logs for easier verification

        // Simulate cash insertion (1500 JPY) at once to avoid race condition
        _fixture.DepositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> {
            { new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), 1 },
            { new DenominationKey(500, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 1 }
        });

        // Act - Trigger completion logic (which is called automatically by subscription)
        // Give it a moment to process the async completion
        await Task.Delay(PosTransactionTestConstants.AsyncCompletionWaitMs);
    }

    private void VerifyCompletionSequence(PosTransactionViewModel vm)
    {
        try
        {
            vm.OposLog.ShouldContain(s => s.Contains("FixDeposit()"));
            vm.OposLog.ShouldContain(s => s.Contains("EndDeposit(NoChange)"));
            vm.OposLog.ShouldContain(s => s.Contains($"DispenseChange({PosTransactionTestConstants.ChangeAmount})"));
            vm.OposLog.ShouldContain(s => s.Contains("Release()"));
            vm.OposLog.ShouldContain(s => s.Contains("Close()"));
        }
        catch (Exception ex)
        {
            var logs = string.Join("\n", vm.OposLog);
            throw new Exception($"Test Failed. OposLog:\n{logs}\n\nOriginal Exception: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
