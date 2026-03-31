using CashChangerSimulator.UI.Tests.Helpers;
using CashChangerSimulator.UI.Tests.Fixtures;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>MainViewModel の初期化と基本的な動作を検証するテストクラス。</summary>
[Collection("SequentialTests")]
public class MainViewModelTests : IDisposable
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public MainViewModelTests()
    {
        _fixture = new UIViewModelFixture();
        _fixture.Initialize();
    }

    /// <summary>MainViewModel の初期状態が正しくセットアップされることを検証します。</summary>
    [Fact]
    public void MainViewModelShouldInitializeCorrectly()
    {
        // Setup
        var vm = _fixture.CreateMainViewModel();

        // Verify: ViewModel is properly initialized
        vm.Deposit.ShouldNotBeNull();
        vm.Dispense.ShouldNotBeNull();
        vm.Inventory.ShouldNotBeNull();

        // Verify: IsInDepositMode is false by default
        vm.Deposit.IsInDepositMode.Value.ShouldBeFalse();

        // Verify: DispenseAmountInput is empty by default
        vm.Dispense.DispenseAmountInput.Value.ShouldBe("");
    }

    /// <summary>リソースを解放します。</summary>
    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
