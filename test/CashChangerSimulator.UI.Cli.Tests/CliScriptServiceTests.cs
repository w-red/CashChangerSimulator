using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device.Services;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliScriptService のスクリプト実行機能を検証するためのテストクラス。</summary>
public class CliScriptServiceTests
{
    private readonly Mock<IScriptExecutionService> _mockScriptExecutionService;
    private readonly IAnsiConsole _console;
    private readonly StringWriter _consoleOutput;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliScriptService _service;

    public CliScriptServiceTests()
    {
        _mockScriptExecutionService = new Mock<IScriptExecutionService>();
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

        _service = new CliScriptService(_mockScriptExecutionService.Object, _console, _mockLocalizer.Object);
    }

    /// <summary>ファイルが存在しない場合、エラーメッセージが表示されることを検証します。</summary>
    [Fact]
    public async Task RunScriptAsyncFileNotFoundShouldPrintErrorMessage()
    {
        // Act
        await _service.RunScriptAsync("non-existent-file.json");

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.file_not_found(non-existent-file.json)");
    }

    /// <summary>有効なファイルの場合、スクリプトが実行され進捗が表示されることを検証します。</summary>
    [Fact]
    public async Task RunScriptAsyncValidFileShouldExecuteAndPrintProgress()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var jsonContent = "{\"script\": \"test\"}";
        await File.WriteAllTextAsync(tempFile, jsonContent);

        try
        {
            _mockScriptExecutionService.Setup(s => s.ExecuteScriptAsync(jsonContent, It.IsAny<Action<string>>()))
                .Callback<string, Action<string>>((json, onOp) => {
                    onOp("TestOperation");
                })
                .Returns(Task.CompletedTask);

            // Act
            await _service.RunScriptAsync(tempFile);

            // Assert
            var output = _consoleOutput.ToString();
            output.ShouldContain("messages.script_executing");
            output.ShouldContain("messages.executing_op(TestOperation)");
            output.ShouldContain("messages.script_completed");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>例外が発生した場合、エラーハンドリングが行われることを検証します。</summary>
    [Fact]
    public async Task RunScriptAsyncExceptionShouldHandleIt()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, "{}");

        try
        {
            _mockScriptExecutionService.Setup(s => s.ExecuteScriptAsync(It.IsAny<string>(), It.IsAny<Action<string>>()))
                .ThrowsAsync(new Exception("Script Failed"));

            // Act
            await _service.RunScriptAsync(tempFile);

            // Assert
            _consoleOutput.ToString().ShouldContain("messages.error_label");
            _consoleOutput.ToString().ShouldContain("Script Failed");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
