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

/// <summary>CliCashService の現金操作機能を検証するためのテストクラス。</summary>
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

    /// <summary>ReadCashCounts 操作でコンソールに表が出力されることを検証します。</summary>
    [Fact]
    public void ReadCashCountsShouldWriteTableToConsole()
    {
        // Arrange
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

    /// <summary>Deposit 操作が BeginDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void DepositShouldInvokeBeginDeposit()
    {
        // Act
        _service.Deposit(1000);

        // Assert
        _mockChanger.Verify(c => c.BeginDeposit(), Times.Once);
    }

    /// <summary>金額 null での Deposit 操作が全額入金として処理されることを検証します。</summary>
    [Fact]
    public void DepositWithNullAmountShouldInvokeBeginDeposit()
    {
        // Act
        _service.Deposit(null);

        // Assert
        _mockChanger.Verify(c => c.BeginDeposit(), Times.Once);
    }

    /// <summary>FixDeposit 操作が FixDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void FixDepositShouldInvokeFixDeposit()
    {
        // Act
        _service.FixDeposit();

        // Assert
        _mockChanger.Verify(c => c.FixDeposit(), Times.Once);
    }

    /// <summary>Dispense 操作が DispenseChange を呼び出すことを検証します。</summary>
    [Fact]
    public void DispenseShouldInvokeDispenseChange()
    {
        // Act
        _service.Dispense(1000);

        // Assert
        _mockChanger.Verify(c => c.DispenseChange(1000), Times.Once);
    }

    /// <summary>EndDeposit 操作が EndDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void EndDepositShouldInvokeEndDeposit()
    {
        // Act
        _service.EndDeposit();

        // Assert
        _mockChanger.Verify(c => c.EndDeposit(CashDepositAction.Change), Times.Once);
    }

    /// <summary>非同期設定での Deposit 操作が適切に開始されることを検証します。</summary>
    [Fact]
    public void DepositAsyncShouldOnlyInvokeBeginDeposit()
    {
        // Arrange
        var options = new CliSessionOptions { IsAsync = true };
        var service = new CliCashService(
            _mockChanger.Object, new Inventory(), _mockMetadata.Object, options, _mockConsole.Object, _mockLocalizer.Object);

        // Act
        service.Deposit(1000);

        // Assert
        _mockChanger.Verify(c => c.BeginDeposit(), Times.Once);
        _mockChanger.Verify(c => c.FixDeposit(), Times.Never);
    }

    /// <summary>正しい形式の AdjustCashCounts 操作が成功することを検証します。</summary>
    [Fact]
    public void AdjustCashCountsShouldInvokeAdjustOnChanger()
    {
        // Act
        _service.AdjustCashCounts("1000:5,500:10");

        // Assert
        _mockChanger.Verify(c => c.AdjustCashCounts(It.IsAny<IEnumerable<CashCount>>()), Times.Once);
    }

    /// <summary>不正な形式の AdjustCashCounts 操作がエラーを報告することを検証します。</summary>
    [Fact]
    public void AdjustCashCountsInvalidFormatShouldReportError()
    {
        // Act
        _service.AdjustCashCounts("invalid");

        // Assert
        _mockChanger.Verify(c => c.AdjustCashCounts(It.IsAny<IEnumerable<CashCount>>()), Times.Never);
        _mockLocalizer.Verify(l => l["messages.invalid_adjust_format"], Times.Once);
    }

    /// <summary>各メソッドで例外が発生した場合に HandleException が呼ばれることを検証するための理論テスト。</summary>
    [Theory]
    [InlineData("ReadCashCounts")]
    [InlineData("Deposit")]
    [InlineData("FixDeposit")]
    [InlineData("EndDeposit")]
    [InlineData("AdjustCashCounts")]
    [InlineData("Dispense")]
    public void MethodsShouldHandleExceptions(string methodName)
    {
        // Arrange
        var exception = new PosControlException("Error", ErrorCode.Failure);
        switch (methodName)
        {
            case "ReadCashCounts": _mockChanger.Setup(c => c.ReadCashCounts()).Throws(exception); break;
            case "Deposit": _mockChanger.Setup(c => c.BeginDeposit()).Throws(exception); break;
            case "FixDeposit": _mockChanger.Setup(c => c.FixDeposit()).Throws(exception); break;
            case "EndDeposit": _mockChanger.Setup(c => c.EndDeposit(It.IsAny<CashDepositAction>())).Throws(exception); break;
            case "AdjustCashCounts": _mockChanger.Setup(c => c.AdjustCashCounts(It.IsAny<IEnumerable<CashCount>>())).Throws(exception); break;
            case "Dispense": _mockChanger.Setup(c => c.DispenseChange(It.IsAny<int>())).Throws(exception); break;
        }

        // Act
        switch (methodName)
        {
            case "ReadCashCounts": _service.ReadCashCounts(); break;
            case "Deposit": _service.Deposit(1000); break;
            case "FixDeposit": _service.FixDeposit(); break;
            case "EndDeposit": _service.EndDeposit(); break;
            case "AdjustCashCounts": _service.AdjustCashCounts("1000:1"); break;
            case "Dispense": _service.Dispense(1000); break;
        }

        // Assert
        _mockLocalizer.Verify(l => l["messages.error_label"], Times.Once);
    }
}
