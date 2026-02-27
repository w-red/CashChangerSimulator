using CashChangerSimulator.Core;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Shouldly;
using CashChangerSimulator.Core.Services;
using Moq;
using R3;
using Xunit;

namespace CashChangerSimulator.UI.Tests;

/// <summary>POS取引画面のViewModelとUI要素の論理的なバインディングを検証するテスト。</summary>
[Collection("StaCollection")]
public class PosTransactionUITest : IDisposable
{
    private readonly PosTransactionViewModelFixture _fixture = new();

    public PosTransactionUITest()
    {
        _fixture.Initialize(PosTransactionTestConstants.TestCurrencyCode);
    }

    /// <summary>TargetAmountInputに対する入力がViewModelに伝播し、StartCommandの実行可能性に影響を与えることを検証します。</summary>
    /// <remarks>
    /// 文字列入力、パース、およびStartCommandのCanExecuteの変化を確認します。
    /// TDD: Stringが入力されるまでStartCommandは無効化されるべきです。
    /// </remarks>
    [Fact]
    public void TargetAmountInputShouldUpdateAndEnableStartCommand()
    {
        // Arrange
        var vm = CreateViewModel();
        
        // Assert: 初期状態ではStartCommandは実行不可
        ((System.Windows.Input.ICommand)vm.StartCommand).CanExecute(null).ShouldBeFalse("初期状態でStartCommandが有効になっています。");

        // Act: 目標金額を入力 (バインディングによる文字列代入をシミュレート)
        vm.TargetAmountInput.Value = "1500";

        // Assert: ViewModelに値が伝播し、StartCommandが実行可能になること
        vm.TargetAmountInput.Value.ShouldBe("1500");
        ((System.Windows.Input.ICommand)vm.StartCommand).CanExecute(null).ShouldBeTrue("有効な金額を入力後、StartCommandが有効になる必要があります。");
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

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
