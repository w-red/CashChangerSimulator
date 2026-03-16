using System.Windows.Input;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Tests.Fixtures;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>POS取引モードの UI ライフサイクルと ViewModel の状態遷移を検証するテストクラス。</summary>
/// <remarks>
/// 金額入力のバリデーション、取引開始から完了までのステータス遷移、タイムアウト処理、
/// および OPOS ログ出力の正確性を UI ロジックの観点から検証します。
/// </remarks>
public class PosTransactionViewModelTest(PosTransactionViewModelFixture fixture) : IClassFixture<PosTransactionViewModelFixture>
{
    private readonly PosTransactionViewModelFixture _fixture = fixture;

    /// <summary>初期化時にトランザクションステータスが Idle になることを検証します。</summary>
    [Fact]
    public void ConstructorShouldInitializeCorrectly()
    {
        var vm = _fixture.CreateViewModel();
        PosTransactionStatus status = vm.TransactionStatus.Value;
        status.ShouldBe(PosTransactionStatus.Idle);
    }

    /// <summary>不正な入力値の場合、取引開始コマンドが実行不可であることを検証します。</summary>
    [Theory]
    [InlineData("abc")]      // 不正な形式
    [InlineData("-1000")]    // 負の値
    [InlineData("")]         // 空文字列
    [InlineData("0")]        // 0円
    public void StartTransactionWithInvalidAmount_ShouldBeDisabled(string invalidAmount)
    {
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = invalidAmount;

        ((ICommand)vm.StartCommand).CanExecute(null).ShouldBeFalse();
    }

    /// <summary>取引開始後、正常に WaitingForCash ステータスに遷移することを検証します。</summary>
    [Fact]
    public void StartTransaction_ShouldTransitionToWaitingForCash()
    {
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = PosTransactionTestConstants.TargetAmount;

        vm.StartCommand.Execute(Unit.Default);

        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);
        vm.OposLog.ShouldContain(s => s.Contains("BeginDeposit()"));
    }

    /// <summary>取引をキャンセルした場合、アイドル状態に戻り、デバイスがクリーンアップされることを検証します。</summary>
    [Fact]
    public void CancelTransaction_ShouldReturnToIdleAndCloseDevice()
    {
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = PosTransactionTestConstants.TargetAmount;
        vm.StartCommand.Execute(Unit.Default);

        vm.CancelCommand.Execute(Unit.Default);

        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
        vm.OposLog.ShouldContain(s => s.Contains("Release()"));
        vm.OposLog.ShouldContain(s => s.Contains("Close()"));
    }

    /// <summary>タイムアウトが発生した場合、自動的にキャンセル処理が走ることを検証します。</summary>
    [Fact]
    public async Task TransactionTimeout_ShouldTriggerCancel()
    {
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = PosTransactionTestConstants.TargetAmount;
        vm.TransactionTimeoutSeconds.Value = 1; // 1秒でタイムアウト

        vm.StartCommand.Execute(Unit.Default);
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);

        // タイムアウトを待機 (1s + 余裕)
        await Task.Delay(1500, TestContext.Current.CancellationToken);

        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
        vm.OposLog.ShouldContain(s => s.Contains("TIMEOUT"));
    }
}
