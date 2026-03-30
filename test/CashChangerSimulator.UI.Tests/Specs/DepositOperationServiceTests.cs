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

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>DepositOperationService の動作を検証するテスト。</summary>
public class DepositOperationServiceTests
{
    private readonly Mock<IDeviceFacade> _mockFacade;
    private readonly Mock<INotifyService> _mockNotify;
    private readonly Mock<ILogger<DepositOperationService>> _mockLogger;
    private readonly DepositOperationService _service;

    public DepositOperationServiceTests()
    {
        _mockFacade = new Mock<IDeviceFacade>();
        _mockNotify = new Mock<INotifyService>();
        _mockLogger = new Mock<ILogger<DepositOperationService>>();
        
        var inventory = new Inventory();
        var hardware = new HardwareStatusManager();
        var mockDeposit = new Mock<DepositController>(inventory, hardware, new Mock<CashChangerManager>(inventory, new Mock<TransactionHistory>().Object, new Mock<ChangeCalculator>().Object, new ConfigurationProvider()).Object, new ConfigurationProvider());
        var mockDispense = new Mock<DispenseController>(new Mock<CashChangerManager>(inventory, new Mock<TransactionHistory>().Object, new Mock<ChangeCalculator>().Object, new ConfigurationProvider()).Object, hardware, new Mock<IDeviceSimulator>().Object);
        _mockFacade.Setup(f => f.Deposit).Returns(mockDeposit.Object);
        _mockFacade.Setup(f => f.Dispense).Returns(mockDispense.Object);
        _mockFacade.Setup(f => f.Status).Returns(hardware);

        _service = new DepositOperationService(_mockFacade.Object, _mockNotify.Object, _mockLogger.Object);
    }

    /// <summary>入金開始メソッドが、正確にファサードの BeginDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void BeginDepositShouldCallFacade()
    {
        // Act
        _service.BeginDeposit();

        // Assert
        _mockFacade.Verify(f => f.Deposit.BeginDeposit(), Times.Once);
    }

    /// <summary>入金開始時に POS 例外が発生した場合、適切に通知サービスが呼び出されることを検証します。</summary>
    [Fact]
    public void BeginDepositShouldHandlePosControlException()
    {
        // Arrange
        var mockDeposit = new Mock<DepositController>(new Inventory(), new HardwareStatusManager(), (CashChangerManager)null!, (ConfigurationProvider)null!);
        mockDeposit.Setup(d => d.BeginDeposit()).Throws(new PosControlException("Error", ErrorCode.Failure));
        _mockFacade.Setup(f => f.Deposit).Returns(mockDeposit.Object);

        // Act
        _service.BeginDeposit();

        // Assert
        _mockNotify.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
