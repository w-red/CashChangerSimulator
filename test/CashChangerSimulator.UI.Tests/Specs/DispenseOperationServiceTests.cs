using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DispenseOperationService の動作を検証するテスト。</summary>
public class DispenseOperationServiceTests
{
    private readonly Mock<IDeviceFacade> _mockFacade;
    private readonly Mock<INotifyService> _mockNotify;
    private readonly Mock<ILogger<DispenseOperationService>> _mockLogger;
    private readonly ConfigurationProvider _configProvider;
    private readonly DispenseOperationService _service;

    public DispenseOperationServiceTests()
    {
        _mockFacade = new Mock<IDeviceFacade>();
        _mockNotify = new Mock<INotifyService>();
        _mockLogger = new Mock<ILogger<DispenseOperationService>>();
        _configProvider = new ConfigurationProvider();
        
        var inventory = new Inventory();
        var hardware = new HardwareStatusManager();
        var mockManager = new Mock<CashChangerManager>(inventory, new TransactionHistory(), new ChangeCalculator(), _configProvider);
        var mockDispense = new Mock<DispenseController>(mockManager.Object, hardware, (IDeviceSimulator)null!);
        _mockFacade.Setup(f => f.Dispense).Returns(mockDispense.Object);
        _mockFacade.Setup(f => f.Status).Returns(hardware);

        _service = new DispenseOperationService(_mockFacade.Object, _mockNotify.Object, _mockLogger.Object, _configProvider);
    }

    [Fact]
    public void DispenseCash_ShouldCallFacade()
    {
        // Act
        _service.DispenseCash(100m);

        // Assert
        _mockFacade.Verify(f => f.Dispense.DispenseChangeAsync(100m, true, It.IsAny<Action<ErrorCode, int>>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ExecuteBulkDispense_ShouldCallFacade()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(100, CurrencyCashType.Bill), 1 } };

        // Act
        _service.ExecuteBulkDispense(counts);

        // Assert
        _mockFacade.Verify(f => f.Dispense.DispenseCashAsync(counts, true, It.IsAny<Action<ErrorCode, int>>()), Times.Once);
    }
}
