using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Shouldly;
using Moq;
using Xunit;
using R3;

namespace CashChangerSimulator.UI.Tests.ViewModels;

/// <summary>POS 取引フローのViewModelレベルでの動作を検証するテストクラス。</summary>
public class PosTransactionFlowTest : IClassFixture<PosTransactionViewModelFixture>
{
    private readonly PosTransactionViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public PosTransactionFlowTest(PosTransactionViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    /// <summary>開始コマンドによってステータスが「現金の投入待ち」に遷移することを検証します。</summary>
    [Fact]
    public void StartCommandShouldTransitionToWaitingForCash()
    {
        // Assemble
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = "1000";

        // Act
        vm.StartCommand.Execute(Unit.Default);

        // Assert
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);
        vm.OposLog.Any(l => l.Contains("BeginDeposit()")).ShouldBeTrue();
    }

    /// <summary>現金を投入し、目標金額に達したときに取引が完了することを検証します。</summary>
    [Fact]
    public async Task InsertCashWhenAmountMetShouldCompleteTransaction()
    {
        // Assemble
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = "1000";
        vm.StartCommand.Execute(Unit.Default);
        var den = vm.AvailableDenominations.First(d => d.Key.Value == 1000);

        // Act
        vm.InsertCashCommand.Execute(den);
        
        // Give some time for async completion logic
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert
        vm.OposLog.Any(l => l.Contains("FixDeposit()")).ShouldBeTrue();
        vm.OposLog.Any(l => l.Contains("EndDeposit(NoChange)")).ShouldBeTrue();
    }

    /// <summary>キャンセルコマンドによって返却が行われ、アイドル状態に戻ることを検証します。</summary>
    [Fact]
    public void CancelCommandShouldRepayAndReturnToIdle()
    {
        // Assemble
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = "1000";
        vm.StartCommand.Execute(Unit.Default);

        // Act
        vm.CancelCommand.Execute(Unit.Default);

        // Assert
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
        vm.OposLog.Any(l => l.Contains("EndDeposit(Repay)")).ShouldBeTrue();
    }

    /// <summary>タイムアウトによって取引が自動的にキャンセルされることを検証します。</summary>
    [Fact]
    public async Task TimeoutShouldCancelTransaction()
    {
        // Assemble
        var vm = _fixture.CreateViewModel();
        vm.TargetAmountInput.Value = "1000";
        vm.TransactionTimeoutSeconds.Value = 1; // 1 second timeout
        vm.StartCommand.Execute(Unit.Default);
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);

        // Act
        await Task.Delay(1500, TestContext.Current.CancellationToken); // Wait for timeout

        // Assert
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.Idle);
        vm.OposLog.Any(l => l.Contains("TIMEOUT")).ShouldBeTrue();
    }

    /// <summary>手動操作コマンドが適切にデバイスメソッドを呼び出すことを検証します。</summary>
    [Fact]
    public void ManualCommandsShouldExecuteOposActions()
    {
        // Assemble
        var vm = _fixture.CreateViewModel();

        // Act
        vm.ManualOpenCommand.Execute(Unit.Default);
        vm.ManualDepositCommand.Execute(Unit.Default);
        vm.ManualDispenseCommand.Execute(Unit.Default);
        vm.ManualCloseCommand.Execute(Unit.Default);

        // Assert
        vm.OposLog.Any(l => l.Contains("Open()")).ShouldBeTrue();
        vm.OposLog.Any(l => l.Contains("BeginDeposit()")).ShouldBeTrue();
        vm.OposLog.Any(l => l.Contains("FixDeposit()")).ShouldBeTrue();
        vm.OposLog.Any(l => l.Contains("Close()")).ShouldBeTrue();
    }
}
