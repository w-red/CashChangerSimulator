using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;
using R3;

namespace CashChangerSimulator.UI.Tests.Specs;

public class BasicViewModelTests
{
    [Fact]
    public void DenominationViewModel_ShouldReflectCorrectState()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var inventory = new Inventory();
        var status = new HardwareStatusManager();
        var config = new ConfigurationProvider();
        var deposit = new DepositController(inventory, status, null, config);
        
        var mockFacade = new Mock<IDeviceFacade>();
        mockFacade.Setup(f => f.Inventory).Returns(inventory);
        mockFacade.Setup(f => f.Deposit).Returns(deposit);
        
        var metadata = new CurrencyMetadataProvider(config);
        var monitor = new CashStatusMonitor(inventory, key, 5, 50, 100);

        var vm = new DenominationViewModel(mockFacade.Object, key, metadata, monitor, config);

        // Act & Assert
        vm.Name.ShouldNotBeNull();
        vm.Key.Value.ShouldBe(1000m);
        vm.Key.Type.ShouldBe(CurrencyCashType.Bill);
        vm.IsDepositable.ShouldBeTrue(); // default
    }

    [Fact]
    public void DenominationViewModel_Dispose_ShouldReleaseResources()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var inventory = new Inventory();
        var status = new HardwareStatusManager();
        var config = new ConfigurationProvider();
        var deposit = new DepositController(inventory, status, null, config);
        
        var mockFacade = new Mock<IDeviceFacade>();
        mockFacade.Setup(f => f.Inventory).Returns(inventory);
        mockFacade.Setup(f => f.Deposit).Returns(deposit);
        
        var metadata = new CurrencyMetadataProvider(config);
        var monitor = new CashStatusMonitor(inventory, key, 5, 50, 100);

        var vm = new DenominationViewModel(mockFacade.Object, key, metadata, monitor, config);

        // Act
        vm.Dispose();

        // Assert: No exception and property still accessible (though it's a simple VM)
        vm.Count.Value.ShouldBe(0);
    }

    [Fact]
    public void MSIServiceProvider_Resolve_ShouldReturnRequiredService()
    {
        // Arrange
        var services = new ServiceCollection();
        var mockService = new Mock<IReadOnlyInventory>();
        services.AddSingleton(mockService.Object);
        var provider = services.BuildServiceProvider();
        
        var msiProvider = new MSIServiceProvider(provider);

        // Act
        var result = msiProvider.Resolve<IReadOnlyInventory>();

        // Assert
        result.ShouldBe(mockService.Object);
    }

    [Fact]
    public void DepositViewModel_DepositStatus_ShouldReflectState()
    {
        // Arrange
        var inventory = new Inventory();
        var status = new HardwareStatusManager();
        var config = new ConfigurationProvider();
        var controller = new DepositController(inventory, status, null, config);
        
        var mockFacade = new Mock<IDeviceFacade>();
        mockFacade.Setup(f => f.Deposit).Returns(controller);
        mockFacade.Setup(f => f.Status).Returns(status);

        var metadata = new CurrencyMetadataProvider(config);

        var vm = new DepositViewModel(
            mockFacade.Object, 
            () => Enumerable.Empty<DenominationViewModel>(),
            new BindableReactiveProperty<bool>(false),
            new Mock<INotifyService>().Object,
            metadata);

        // Act & Assert
        vm.DepositStatus.Value.ShouldBe(CashDepositStatus.None);
    }

    [Fact]
    public void CurrencyMetadataProvider_Symbol_ShouldReturnCorrectValue()
    {
        // Arrange
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = "JPY";
        config.Config.System.CultureCode = "ja-JP";
        var metadata = new CurrencyMetadataProvider(config);

        // Act & Assert
        metadata.Symbol.ShouldBe("円");
        metadata.CurrencyCode.ShouldBe("JPY");
    }

    [Fact]
    public void CurrencyMetadataProvider_GetDenominationName_EUR_ShouldWork()
    {
        // Arrange
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = "EUR";
        var metadata = new CurrencyMetadataProvider(config);
        var key = new DenominationKey(50, CurrencyCashType.Bill, "EUR");

        // Act
        var name = metadata.GetDenominationName(key, "en-US");

        // Assert
        name.ShouldBe("€50 Note");
    }
}
