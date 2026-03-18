using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using Microsoft.PointOfService;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliServiceBase の機能を検証するためのテストクラス。</summary>
public class CliServiceBaseTests
{
    private readonly IAnsiConsole _console;
    private readonly StringWriter _consoleOutput;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly TestCliService _service;

    public CliServiceBaseTests()
    {
        _consoleOutput = new StringWriter();
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(_consoleOutput)
        });

        _mockLocalizer = new Mock<IStringLocalizer>();
        
        // Mock localizer to return the key as the localized value, including arguments if any
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args == null || args.Length == 0 ? s : $"{s}({string.Join(", ", args)})"));

        _service = new TestCliService(_console, _mockLocalizer.Object);
    }

    private class TestCliService(IAnsiConsole console, IStringLocalizer localizer) 
        : CliServiceBase(console, localizer)
    {
    }

    /// <summary>メッセージが null の場合、成功ラベルのみが出力されることを検証します。</summary>
    [Fact]
    public void ReportSuccessWithNullMessageShouldWriteLabelOnly()
    {
        // Act
        _service.ReportSuccess(null);

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.success_label");
    }

    /// <summary>メッセージが指定された場合、ラベルとメッセージの両方が出力されることを検証します。</summary>
    [Fact]
    public void ReportSuccessWithNonEmptyMessageShouldWriteLabelAndMessage()
    {
        // Act
        _service.ReportSuccess("Operation Completed");

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.success_label");
        output.ShouldContain("Operation Completed");
    }

    /// <summary>PosControlException が発生した場合、詳細なエラー情報が出力されることを検証します。</summary>
    [Fact]
    public void HandleExceptionPosControlExceptionShouldWriteDetailedInformation()
    {
        // Arrange
        var ex = new PosControlException("Hardware Error", ErrorCode.Timeout, 5);

        // Act
        _service.HandleException(ex);

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.error_label");
        output.ShouldContain("Hardware Error");
        output.ShouldContain("messages.summary_label");
        output.ShouldContain("messages.error_summary_timeout");
        output.ShouldContain("messages.code_label");
        output.ShouldContain("Timeout");
        output.ShouldContain("5");
    }

    /// <summary>ヒントが存在するエラーの場合、ヒント情報が出力されることを検証します。</summary>
    [Fact]
    public void HandleExceptionPosControlExceptionWithHintShouldWriteHint()
    {
        // Arrange
        var ex = new PosControlException("Access Denied", ErrorCode.Closed);

        // Act
        _service.HandleException(ex);

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.hint_format");
        output.ShouldContain("messages.error_hint_closed");
    }

    /// <summary>一般的な例外が発生した場合、シンプルなエラーメッセージが出力されることを検証します。</summary>
    [Fact]
    public void HandleExceptionGenericExceptionShouldWriteSimpleErrorMessage()
    {
        // Arrange
        var ex = new Exception("Critical System Error");

        // Act
        _service.HandleException(ex);

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("messages.error_label");
        output.ShouldContain("Critical System Error");
    }

    /// <summary>エラーサマリのキーが見つからない場合、汎用サマリが返されることを検証します。</summary>
    [Fact]
    public void GetSummaryWhenKeyNotFoundShouldReturnGenericSummary()
    {
        // Arrange
        _mockLocalizer.Setup(l => l["messages.error_summary_illegal"]).Returns(new LocalizedString("messages.error_summary_illegal", "messages.error_summary_illegal", true));
        _mockLocalizer.Setup(l => l["messages.error_summary_generic"]).Returns(new LocalizedString("messages.error_summary_generic", "Generic Error Summary"));

        // Act
        _service.HandleException(new PosControlException("Illegal Op", ErrorCode.Illegal));

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("Generic Error Summary");
    }
}
