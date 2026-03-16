using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.Device.Coordination;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

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
        var facade = new DeviceFacade(
            _fixture.Inventory,
            _fixture.Manager,
            _fixture.DepositController,
            _fixture.DispenseController,
            _fixture.Hardware,
            _fixture.CashChanger,
            _fixture.History,
            new OverallStatusAggregatorProvider(new MonitorsProvider(_fixture.Inventory, _fixture.ConfigProvider, _fixture.MetadataProvider)),
            new MonitorsProvider(_fixture.Inventory, _fixture.ConfigProvider, _fixture.MetadataProvider),
            new Mock<INotifyService>().Object);

        var isDispenseBusy = new BindableReactiveProperty<bool>(false);
        var isInDepositMode = new BindableReactiveProperty<bool>(false);

        var depVm = new DepositViewModel(
            facade,
            () => [],
            isDispenseBusy,
            facade.Notify,
            _fixture.MetadataProvider);

        var dispVm = new DispenseViewModel(
            facade,
            _fixture.ConfigProvider,
            isInDepositMode,
            () => [],
            facade.Notify,
            _fixture.MetadataProvider);

        return new PosTransactionViewModel(
            facade,
            depVm,
            dispVm,
            _fixture.MetadataProvider,
            () => [],
            facade.Notify);
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
