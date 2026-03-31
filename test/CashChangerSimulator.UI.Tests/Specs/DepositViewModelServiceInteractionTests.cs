using CashChangerSimulator.UI.Tests.Fixtures;
using Microsoft.PointOfService;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DepositViewModel と IDepositOperationService の相互作用を検証するテスト。</summary>
public class DepositViewModelServiceInteractionTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    public DepositViewModelServiceInteractionTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    /// <summary>入金開始コマンドが、対応するサービスメソッドを呼び出すことを検証します。</summary>
    [Fact]
    public void BeginDepositCommandShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel(depositService: _fixture.DepositServiceMock.Object);
        _fixture.DepositServiceMock.Reset();

        // Act
        vm.BeginDepositCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.BeginDeposit(), Times.Once);
    }

    /// <summary>計数終了（確定）コマンドが、対応するサービスメソッドを呼び出すことを検証します。</summary>
    [Fact]
    public void FixDepositCommandShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel(depositService: _fixture.DepositServiceMock.Object);
        _fixture.DepositServiceMock.Reset();
        // Force state to allow Fix
        vm.IsInDepositMode.Value = true;

        // Act
        vm.FixDepositCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.FixDeposit(), Times.Once);
    }

    /// <summary>収納コマンドが、対応するサービスメソッドを呼び出すことを検証します。</summary>
    [Fact]
    public void StoreDepositNoChangeCommandShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel(depositService: _fixture.DepositServiceMock.Object);
        _fixture.DepositServiceMock.Reset();
        // Force state to allow Store
        vm.IsDepositFixed.Value = true;

        // Act
        vm.StoreDepositNoChangeCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.EndDeposit(CashDepositAction.NoChange), Times.Once);
    }
}
