using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.PointOfService;
using Moq;
using R3;
using Xunit;

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

    [Fact]
    public void BeginDepositCommand_ShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel();
        _fixture.DepositServiceMock.Reset();

        // Act
        vm.BeginDepositCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.BeginDeposit(), Times.Once);
    }

    [Fact]
    public void FixDepositCommand_ShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel();
        _fixture.DepositServiceMock.Reset();
        // Force state to allow Fix
        vm.IsInDepositMode.Value = true;

        // Act
        vm.FixDepositCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.FixDeposit(), Times.Once);
    }

    [Fact]
    public void StoreDepositCommand_ShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDepositViewModel();
        _fixture.DepositServiceMock.Reset();
        // Force state to allow Store
        vm.IsDepositFixed.Value = true;

        // Act
        vm.StoreDepositCommand.Execute(Unit.Default);

        // Assert
        _fixture.DepositServiceMock.Verify(s => s.EndDeposit(CashDepositAction.NoChange), Times.Once);
    }
}
