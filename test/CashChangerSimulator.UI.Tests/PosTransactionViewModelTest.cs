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

    /// <summary>取引開始時のOPOSシーケンス呼び出しを検証します。</summary>
    /// <remarks>
    /// テストフロー:
    /// 1. 取引目標金額をセット
    /// 2. 取引開始コマンドを実行
    /// 
    /// 期待値: 初期化および入金開始シーケンス（Open, Claim, BeginDeposit）が正しく呼ばれること。
    /// </remarks>
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
        VerifyOposLogSequence(vm, "Open()", "Claim(1000)", "BeginDeposit()");
    }

    /// <summary>取引完了時のOPOSシーケンス呼び出しを検証します。</summary>
    /// <remarks>
    /// テストフロー:
    /// 1. 取引を開始状態にする
    /// 2. 支払い金額に達するまでの現金を投入する
    /// 3. 取引完了を待機し、お釣りの支払いを実行
    /// 
    /// 期待値: 預り金確定、釣銭支払い、デバイス解放（EndDeposit, DispenseChange, Release, Close）が正しく呼ばれること。
    /// </remarks>
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
        VerifyOposLogSequence(vm, 
            "FixDeposit()", 
            "EndDeposit(NoChange)", 
            $"DispenseChange({PosTransactionTestConstants.ChangeAmount})", 
            "Release()", 
            "Close()");
    }

    private void VerifyOposLogSequence(PosTransactionViewModel vm, params string[] expectedMessages)
    {
        foreach (var expectedMessage in expectedMessages)
        {
            try
            {
                vm.OposLog.ShouldContain(s => s.Contains(expectedMessage));
            }
            catch (Exception ex)
            {
                var logs = string.Join("\n", vm.OposLog);
                throw new Exception(
                    $"OPOS ログに期待されるメッセージが見つかりません。\n\n" +
                    $"期待メッセージ: {expectedMessage}\n\n" +
                    $"実際のログ:\n{logs}", 
                    ex);
            }
        }
    }

    /// <summary>不正な金額入力時の拒否動作を検証します。</summary>
    /// <remarks>
    /// 期待値: バリデーションエラーにより開始プロセスが進まないこと。
    /// </remarks>
    [Theory]
    [InlineData("abc")]      // 不正な形式
    [InlineData("-1000")]    // 負の値
    [InlineData("")]         // 空文字列
    [InlineData("0")]        // 0円
    public void StartTransactionWithInvalidAmount_ShouldReject(string invalidAmount)
    {
        // Arrange
        var depVm = new DepositViewModel(_fixture.DepositController, _fixture.Hardware, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        var vm = new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger);

        // Act
        vm.TargetAmountInput.Value = invalidAmount;
        
        // Execute manually and expect it to fail, but the right way is to verify CanExecute
        vm.StartCommand.CanExecute().ShouldBeFalse();
    }

    /// <summary>極端に大きな金額入力時の拒否動作を検証します。</summary>
    /// <remarks>
    /// 現状の実装では正の数なら許可されるが、ビジネスロジック的に制限すべきか検討材料。
    /// </remarks>
    [Fact]
    public void StartTransactionWithExtremelyLargeAmount_ShouldReject()
    {
        var depVm = new DepositViewModel(_fixture.DepositController, _fixture.Hardware, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        var vm = new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger);

        vm.TargetAmountInput.Value = "100000001"; // 1億1円
        
        vm.StartCommand.CanExecute().ShouldBeFalse();
    }

    /// <summary>一部支払い後の取引キャンセル動作を検証します。</summary>
    /// <remarks>
    /// 期待値: 
    /// 1. FixDeposit() が呼ばれる。
    /// 2. EndDeposit(Repay) が呼ばれ、投入済みの現金が返却される。
    /// 3. デバイス解放（Release, Close）が呼ばれ、ステータスが待機中に戻る。
    /// </remarks>
    [Fact]
    public void TransactionCancelledAfterPartialPayment_ShouldRepayAndClose()
    {
        // Arrange
        var depVm = new DepositViewModel(_fixture.DepositController, _fixture.Hardware, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        var vm = new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger);

        vm.TargetAmountInput.Value = "2000";
        vm.StartCommand.Execute(Unit.Default);

        // 1000円だけ投入
        _fixture.DepositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> {
            { new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), 1 }
        });
        
        vm.OposLog.Clear(); // Clear logs for easier verification

        // Act
        vm.CancelCommand.Execute(Unit.Default);

        // Verify
        VerifyOposLogSequence(vm, 
            "FixDeposit()", 
            "EndDeposit(Repay)", 
            "Release()", 
            "Close()");
        
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
    }

    /// <summary>タイムアウトによる自動キャンセル動作を検証します。</summary>
    /// <remarks>
    /// 期待値: 指定時間経過後に CancelTransaction が実行されること。
    /// </remarks>
    [Fact]
    public async Task TransactionShouldTimeout_AfterSpecifiedPeriod()
    {
        // Arrange
        var depVm = new DepositViewModel(_fixture.DepositController, _fixture.Hardware, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(_fixture.Inventory, _fixture.Manager, _fixture.DispenseController, _fixture.ConfigProvider, Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        var vm = new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger);

        // テスト用に短いタイムアウトを設定
        vm.TransactionTimeoutSeconds.Value = 1; 

        vm.TargetAmountInput.Value = "1000";
        vm.StartCommand.Execute(Unit.Default);
        vm.OposLog.Clear();

        // Act
        // タイムアウト設定が1秒なら、2秒待てばキャンセルされているはず
        await Task.Delay(2000); 

        // Verify
        VerifyOposLogSequence(vm, "FixDeposit()", "EndDeposit(Repay)", "Close()");
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
