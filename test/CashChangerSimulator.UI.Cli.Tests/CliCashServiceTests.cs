using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.UI.Cli.Tests;

public class CliCashServiceTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly CliCashService _service;

    public CliCashServiceTests()
    {
        _mockChanger = new Mock<SimulatorCashChanger>(new CashChangerSimulator.Device.Coordination.SimulatorDependencies());
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        
        // Setup metadata with R3 properties
        _mockMetadata.Setup(m => m.CurrencyCode).Returns("JPY");
        
        var prefix = new ReactiveProperty<string>("¥");
        _mockMetadata.Setup(m => m.SymbolPrefix).Returns(prefix);
        
        var suffix = new ReactiveProperty<string>("");
        _mockMetadata.Setup(m => m.SymbolSuffix).Returns(suffix);
        
        var denominations = new List<DenominationKey>
        {
            new DenominationKey(1000, CurrencyCashType.Bill, "JPY"),
            new DenominationKey(500, CurrencyCashType.Bill, "JPY")
        };
        _mockMetadata.Setup(m => m.SupportedDenominations).Returns(denominations);

        // Mock localizer to return keys
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        _service = new CliCashService(
            _mockChanger.Object,
            new Inventory(), // Real inventory for total calculation
            _mockMetadata.Object,
            new CliSessionOptions(),
            _mockConsole.Object,
            _mockLocalizer.Object);
    }

    [Fact]
    public void ReadCashCounts_ShouldWriteTableToConsole()
    {
        // Arrange
        // Microsoft.PointOfService.CashCount uses its own enum
        var counts = new CashCounts(new[] { 
            new CashCount(CashCountType.Bill, 1000, 10), 
            new CashCount(CashCountType.Bill, 500, 5) 
        }, false);
        _mockChanger.Setup(c => c.ReadCashCounts()).Returns(counts);

        // Act
        _service.ReadCashCounts();

        // Assert
        _mockChanger.Verify(c => c.ReadCashCounts(), Times.Once);
        _mockConsole.Verify(c => c.Write(It.IsAny<Table>()), Times.Once);
    }

    [Fact]
    public void Deposit_ShouldInvokeBeginDeposit()
    {
        // Act
        _service.Deposit(1000);

        // Assert
        _mockChanger.Verify(c => c.BeginDeposit(), Times.Once);
    }

    [Fact]
    public void FixDeposit_ShouldInvokeFixDeposit()
    {
        // Act
        _service.FixDeposit();

        // Assert
        _mockChanger.Verify(c => c.FixDeposit(), Times.Once);
    }

    [Fact]
    public void Dispense_ShouldInvokeDispenseChange()
    {
        // Act
        _service.Dispense(1000);

        // Assert
        _mockChanger.Verify(c => c.DispenseChange(1000), Times.Once);
    }
}
