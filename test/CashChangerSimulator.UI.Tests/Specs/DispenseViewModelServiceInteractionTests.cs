using CashChangerSimulator.UI.Tests.Fixtures;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DispenseViewModel と IDispenseOperationService の相互作用を検証するテスト。</summary>
public class DispenseViewModelServiceInteractionTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    public DispenseViewModelServiceInteractionTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public void DispenseCommand_ShouldCallService()
    {
        // Arrange
        var vm = _fixture.CreateDispenseViewModel(dispenseService: _fixture.DispenseServiceMock.Object);
        vm.DispenseAmountInput.Value = "100";
        _fixture.DispenseServiceMock.Reset();

        // Act
        vm.DispenseCommand.Execute(Unit.Default);

        // Assert
        _fixture.DispenseServiceMock.Verify(s => s.DispenseCash(100m), Times.Once);
    }
}
