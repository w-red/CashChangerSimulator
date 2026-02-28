using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>Test class for providing PosTransactionViewModelTest functionality.</summary>
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
        var vm = CreateViewModel();

        vm.TargetAmountInput.Value =
            PosTransactionTestConstants.TargetAmount;

        // Act
        vm.StartCommand.Execute(Unit.Default);

        // Verify
        vm.TransactionStatus.Value
            .ShouldBe(PosTransactionStatus.WaitingForCash);
        VerifyOposLogSequence(
            vm,
            "Open()",
            "Claim(1000)",
            "BeginDeposit()");
    }

    private PosTransactionViewModel CreateViewModel()
    {
        var notifyService = new Mock<INotifyService>().Object;
        var isDispenseBusy = new BindableReactiveProperty<bool>(false);
        var isInDepositMode = new BindableReactiveProperty<bool>(false);

        var depVm = new DepositViewModel(
            _fixture.DepositController,
            _fixture.Hardware,
            () => [],
            isDispenseBusy,
            notifyService,
            _fixture.MetadataProvider);
        var dispVm = new DispenseViewModel(
            _fixture.Inventory,
            _fixture.Manager,
            _fixture.DispenseController,
            _fixture.Hardware,
            _fixture.ConfigProvider,
            isInDepositMode,
            () => [],
            notifyService,
            _fixture.MetadataProvider);
        
        return new PosTransactionViewModel(depVm, dispVm, _fixture.CashChanger, _fixture.MetadataProvider);
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
        var vm = CreateViewModel();

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
        await Task.Delay(PosTransactionTestConstants.AsyncCompletionWaitMs, TestContext.Current.CancellationToken);
    }

    private static void VerifyCompletionSequence(PosTransactionViewModel vm)
    {
        VerifyOposLogSequence(vm, 
            "FixDeposit()", 
            "EndDeposit(NoChange)", 
            $"DispenseChange({PosTransactionTestConstants.ChangeAmount})", 
            "Release()", 
            "Close()");
    }

    private static void VerifyOposLogSequence(
        PosTransactionViewModel vm,
        params string[] expectedMessages)
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
    public void StartTransactionWithInvalidAmountShouldReject(string invalidAmount)
    {
        // Arrange
        var vm = CreateViewModel();

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
    public void StartTransactionWithExtremelyLargeAmountShouldReject()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.TargetAmountInput.Value = "100000001"; // 1億1円
        
        // Assert
        vm.StartCommand.CanExecute().ShouldBeFalse();
    }

    /// <summary>プロパティが適切にマップされ、バインディング例外を防ぐことを検証します。</summary>
    [Fact]
    public void PropertiesShouldInitializeAndMapCorrectly()
    {
        using var vm = CreateViewModel();
        
        // Assert initial states
        vm.TotalTargetAmount.CurrentValue.ShouldBe(0m);
        vm.CurrentAmount.CurrentValue.ShouldBe(0m);
        vm.Progress.CurrentValue.ShouldBe(0.0);
        vm.StatusText.CurrentValue.ShouldBe("Ready");
        vm.Message.CurrentValue.ShouldNotBeEmpty();

        // Act
        vm.TargetAmountInput.Value = "1000";
        vm.StartCommand.Execute(Unit.Default); // Start the transaction to accept deposits

        _fixture.DepositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> {
            { new DenominationKey(500, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 1 }
        });

        // Assert updated states safely mapped from string inputs
        vm.TotalTargetAmount.CurrentValue.ShouldBe(1000m);
        vm.CurrentAmount.CurrentValue.ShouldBe(500m);
        vm.Progress.CurrentValue.ShouldBe(50.0);
    }

    /// <summary>一部支払い後の取引キャンセル動作を検証します。</summary>
    /// <remarks>
    /// 期待値: 
    /// 1. FixDeposit() が呼ばれる。
    /// 2. EndDeposit(Repay) が呼ばれ、投入済みの現金が返却される。
    /// 3. デバイス解放（Release, Close）が呼ばれ、ステータスが待機中に戻る。
    /// </remarks>
    [Fact]
    public void TransactionCancelledAfterPartialPaymentShouldRepayAndClose()
    {
        // Arrange
        var vm = CreateViewModel();

        vm.TargetAmountInput.Value = "2000";
        vm.StartCommand.Execute(Unit.Default);

        // 1000円だけ投入
        _fixture.DepositController.TrackBulkDeposit(
            new Dictionary<DenominationKey, int> {
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



    /// <inheritdoc/>
    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}