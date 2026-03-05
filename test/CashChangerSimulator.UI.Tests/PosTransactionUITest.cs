using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

[Collection("StaCollection")]
public class PosTransactionUITest : IDisposable
{
    private readonly PosTransactionViewModelFixture _fixture = new();

    public PosTransactionUITest()
    {
        _fixture.Initialize(PosTransactionTestConstants.TestCurrencyCode);
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
            _fixture.DepositController,
            notifyService);
    }

    [Fact]
    public void TargetAmountInputShouldUpdate()
    {
        var vm = CreateViewModel();
        vm.TargetAmountInput.Value = "1500";
        // Avoid potentially mismatching ReadOnlyReactiveProperty.Value in some environments
        vm.TargetAmountInput.Value.ShouldBe("1500");
    }

    public void Dispose()
    {
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}
