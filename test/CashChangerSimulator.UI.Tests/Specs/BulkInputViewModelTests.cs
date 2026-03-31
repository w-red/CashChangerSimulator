using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>BulkAmountInputViewModel の動作を検証するテストクラス。</summary>
[Collection("SequentialTests")]
public class BulkInputViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public BulkInputViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
    }

    /// <summary>プロパティがコンストラクタ引数と一致することを検証します。</summary>
    [Fact]
    public void PropertiesShouldMatchConstructorArguments()
    {
        // Assemble
        var key = TestConstants.Key1000;
        var items = new List<BulkAmountInputItemViewModel>
        {
            new(key, "1000円札")
        };
        var mockOverlap = new Mock<ICommand>();
        var mockJam = new Mock<ICommand>();
        var mockReset = new Mock<ICommand>();
        var mockDeviceError = new Mock<ICommand>();
        var isJammed = new ReactiveProperty<bool>(false).ToReadOnlyReactiveProperty();
        var isOverlapped = new ReactiveProperty<bool>(false).ToReadOnlyReactiveProperty();
        var isDeviceError = new ReactiveProperty<bool>(false).ToReadOnlyReactiveProperty();

        // Act
        var vm = new BulkAmountInputViewModel(
            items,
            mockOverlap.Object,
            mockJam.Object,
            mockDeviceError.Object,
            mockReset.Object,
            isJammed,
            isOverlapped,
            isDeviceError);

        // Assert
        vm.Items.ShouldBe(items);
        vm.SimulateOverlapCommand.ShouldBe(mockOverlap.Object);
        vm.SimulateJamCommand.ShouldBe(mockJam.Object);
        vm.SimulateDeviceErrorCommand.ShouldBe(mockDeviceError.Object);
        vm.ResetErrorCommand.ShouldBe(mockReset.Object);
        vm.IsJammed.ShouldBe(isJammed);
        vm.IsOverlapped.ShouldBe(isOverlapped);
        vm.IsDeviceError.ShouldBe(isDeviceError);
    }

    /// <summary>金種アイテムの数量がリアクティブであることを検証します。</summary>
    [Fact]
    public void ItemQuantityShouldBeObservable()
    {
        // Assemble
        var key = TestConstants.Key500;
        var item = new BulkAmountInputItemViewModel(key, "500円玉");

        // Act
        item.Quantity.Value = 10;

        // Assert
        item.Quantity.Value.ShouldBe(10);
    }
}
