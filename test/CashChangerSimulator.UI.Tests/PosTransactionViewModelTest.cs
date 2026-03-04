using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Tests.Fixtures;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

public class PosTransactionViewModelTest : IClassFixture<PosTransactionViewModelFixture>
{
    private readonly PosTransactionViewModelFixture _fixture;

    public PosTransactionViewModelTest(PosTransactionViewModelFixture fixture)
    {
        _fixture = fixture;
    }

    private PosTransactionViewModel CreateViewModel()
    {
        var isDispenseBusy = new BindableReactiveProperty<bool>(false);
        var isInDepositMode = new BindableReactiveProperty<bool>(false);
        var notifyService = new Mock<INotifyService>().Object;

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

        return new PosTransactionViewModel(
            depVm, 
            dispVm, 
            _fixture.CashChanger, 
            _fixture.Hardware, 
            _fixture.MetadataProvider, 
            () => [], 
            _fixture.DepositController);
    }

    [Fact]
    public void ConstructorShouldInitializeCorrectly()
    {
        var vm = CreateViewModel();
        PosTransactionStatus status = vm.TransactionStatus.Value;
        status.ShouldBe(PosTransactionStatus.Idle);
    }
}
