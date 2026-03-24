using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using R3;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliViewService の表示機能を検証するためのテストクラス。</summary>
public class CliViewServiceTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Inventory _inventory;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly TransactionHistory _history;
    private readonly IAnsiConsole _console;
    private readonly StringWriter _consoleOutput;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<IHistoryExportService> _mockExportService;
    private readonly CliViewService _service;

    public CliViewServiceTests()
    {
        _mockChanger = new Mock<SimulatorCashChanger>(new CashChangerSimulator.Device.Coordination.SimulatorDependencies());
        _inventory = new Inventory();
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        _history = new TransactionHistory();
        
        _consoleOutput = new StringWriter();
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(_consoleOutput)
        });
        _mockLocalizer = new Mock<IStringLocalizer>();

        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args == null || args.Length == 0 ? s : $"{s}({string.Join(", ", args)})"));

        // Setup metadata
        _mockMetadata.Setup(m => m.CurrencyCode).Returns("JPY");
        _mockMetadata.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(1000, CurrencyCashType.Bill, "JPY")]);
        _mockMetadata.Setup(m => m.SymbolPrefix).Returns(Observable.Return("YEN_PREFIX").ToReadOnlyReactiveProperty<string>());
        _mockMetadata.Setup(m => m.SymbolSuffix).Returns(Observable.Return("YEN_SUFFIX").ToReadOnlyReactiveProperty<string>());

        _mockExportService = new Mock<IHistoryExportService>();
        _service = new CliViewService(_mockChanger.Object, _inventory, _mockMetadata.Object, _history, _mockExportService.Object, _console, _mockLocalizer.Object);
    }

    /// <summary>Status 操作でデバイスの状態と在庫情報が表示されることを検証します。</summary>
    [Fact]
    public void StatusShouldPrintStateAndInventory()
    {
        // Arrange
        _mockChanger.Setup(c => c.State).Returns(ControlState.Idle);
        _mockChanger.Setup(c => c.DeviceEnabled).Returns(true);
        _inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 5);

        // Act
        _service.Status();

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.status_header");
        output.ShouldContain("messages.state_label: Idle");
        output.ShouldContain("messages.enabled_label: True");
        output.ShouldContain("B1000"); // From DenominationKey.ToDenominationString()
        output.ShouldContain("5");
        output.ShouldContain("messages.total_caption: YEN_PREFIX5,000YEN_SUFFIX");
    }

    /// <summary>History 操作で最近の取引履歴が表示されることを検証します。</summary>
    [Fact]
    public void HistoryShouldPrintRecentEntries()
    {
        // Arrange
        _history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 1000, new Dictionary<DenominationKey, int>()));

        // Act
        _service.History(10);

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.transaction_history_header(10)");
        output.ShouldContain("Deposit");
        output.ShouldContain("1,000");
        output.ShouldContain("JPY");
    }

    /// <summary>ExportHistory 操作がエクスポートサービスを呼び出し、成功メッセージを表示することを検証します。</summary>
    [Fact]
    public void ExportHistoryShouldCallServiceAndPrintSuccess()
    {
        // Arrange
        var path = "test.csv";
        _mockExportService.Setup(s => s.Export(It.IsAny<IEnumerable<TransactionEntry>>())).Returns("csv content");

        // Act
        _service.ExportHistory(path);

        // Assert
        _mockExportService.Verify(s => s.Export(It.IsAny<IEnumerable<TransactionEntry>>()), Times.Once);
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.export_success(test.csv)");
        File.Exists(path).ShouldBeTrue();
        File.ReadAllText(path).ShouldBe("csv content");

        // Cleanup
        if (File.Exists(path)) File.Delete(path);
    }
}
