using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Tests.Fixtures;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

public class DenominationViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    public DenominationViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
    }

    [Fact]
    public void BreakdownProperties_ShouldUpdate_WhenInventoryChanges()
    {
        // Arrange
        var inv = _fixture.Inventory;
        var key = TestConstants.Key1000;
        var metadataProvider = _fixture.MetadataProvider;
        var configProvider = _fixture.ConfigProvider;
        var monitor = new CashStatusMonitor(inv, key, 5, 100, 200);
        
        var vm = new DenominationViewModel(_fixture.CreateFacade(), key, metadataProvider, monitor, configProvider);

        // Act & Assert: Recyclable (Normal)
        inv.Add(key, 5);
        vm.Count.Value.ShouldBe(5);
        vm.RecyclableCount.Value.ShouldBe(5);

        // Act & Assert: Collection (Overflow)
        inv.AddCollection(key, 3);
        vm.Count.Value.ShouldBe(8);
        vm.CollectionCount.Value.ShouldBe(3);

        // Act & Assert: Reject
        inv.AddReject(key, 2);
        vm.Count.Value.ShouldBe(10);
        vm.RejectCount.Value.ShouldBe(2);
    }
}
