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
using R3;

namespace CashChangerSimulator.UI.Tests.Specs;

public class BasicViewModelTests
{
    /// <summary>DenominationViewModel が初期状態において正しい名称、金額、金種タイプを保持していることを検証します。</summary>
    [Fact]
    public void DenominationViewModelShouldReflectCorrectState()
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

        var mockDispatcher = new Mock<IDispatcherService>();
        mockDispatcher.Setup(d => d.SafeInvoke(It.IsAny<Action>())).Callback<Action>(a => a());
        mockFacade.Setup(f => f.Dispatcher).Returns(mockDispatcher.Object);
        
        var metadata = new CurrencyMetadataProvider(config);
        var monitor = new CashStatusMonitor(inventory, key, 5, 50, 100);

        var vm = new DenominationViewModel(mockFacade.Object, key, metadata, monitor, config);

        // Act & Assert
        vm.Name.ShouldNotBeNull();
        vm.Key.Value.ShouldBe(1000m);
        vm.Key.Type.ShouldBe(CurrencyCashType.Bill);
        vm.IsDepositable.ShouldBeTrue(); // default
    }

    /// <summary>DenominationViewModel を破棄した際、リソースが正しく解放されることを検証します。</summary>
    [Fact]
    public void DenominationViewModelDisposeShouldReleaseResources()
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

        var mockDispatcher = new Mock<IDispatcherService>();
        mockDispatcher.Setup(d => d.SafeInvoke(It.IsAny<Action>())).Callback<Action>(a => a());
        mockFacade.Setup(f => f.Dispatcher).Returns(mockDispatcher.Object);
        
        var metadata = new CurrencyMetadataProvider(config);
        var monitor = new CashStatusMonitor(inventory, key, 5, 50, 100);

        var vm = new DenominationViewModel(mockFacade.Object, key, metadata, monitor, config);

        // Act
        vm.Dispose();

        // Assert: No exception and property still accessible (though it's a simple VM)
        vm.Count.Value.ShouldBe(0);
    }

    /// <summary>MSIServiceProvider が、登録されたサービスを正しく解決できることを検証します。</summary>
    [Fact]
    public void MSIServiceProviderResolveShouldReturnRequiredService()
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

    /// <summary>DepositViewModel の入金ステータスが、初期状態で None であることを検証します。</summary>
    [Fact]
    public void DepositViewModelDepositStatusShouldReflectState()
    {
        // Arrange
        var inventory = new Inventory();
        var status = new HardwareStatusManager();
        var config = new ConfigurationProvider();
        var controller = new DepositController(inventory, status, null, config);
        
        var mockFacade = new Mock<IDeviceFacade>();
        mockFacade.Setup(f => f.Deposit).Returns(controller);
        mockFacade.Setup(f => f.Status).Returns(status);

        var mockDispatcher = new Mock<IDispatcherService>();
        mockDispatcher.Setup(d => d.SafeInvoke(It.IsAny<Action>())).Callback<Action>(a => a());
        mockFacade.Setup(f => f.Dispatcher).Returns(mockDispatcher.Object);

        var metadata = new CurrencyMetadataProvider(config);

        var vm = new DepositViewModel(
            mockFacade.Object, 
            () => Enumerable.Empty<DenominationViewModel>(),
            new BindableReactiveProperty<bool>(false),
            new Mock<IDepositOperationService>().Object,
            new Mock<IInventoryOperationService>().Object,
            metadata,
            mockDispatcher.Object);

        // Act & Assert
        vm.DepositStatus.Value.ShouldBe(CashDepositStatus.None);
    }

    /// <summary>CurrencyMetadataProvider が、設定に応じた正しい通貨記号と通貨コードを返すことを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderSymbolShouldReturnCorrectValue()
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

    /// <summary>CurrencyMetadataProvider が、ユーロ(EUR)の金種名称を正しく生成できることを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderGetDenominationNameEURShouldWork()
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
