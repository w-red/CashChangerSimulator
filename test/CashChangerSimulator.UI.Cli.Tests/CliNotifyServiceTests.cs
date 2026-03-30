using Shouldly;
using Spectre.Console.Testing;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CLI の通知サービスを検証するテストクラス。</summary>
public class CliNotifyServiceTests
{
    private readonly TestConsole _console;
    private readonly CliNotifyService _sut;

    public CliNotifyServiceTests()
    {
        _console = new TestConsole();
        _sut = new CliNotifyService(_console);
    }

    /// <summary>情報メッセージが適切なマークアップで出力されることを検証します。</summary>
    [Fact]
    public void ShowInfoShouldOutputBlueMarkup()
    {
        // Act
        _sut.ShowInfo("System initialized.", "INFO");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[INFO] System initialized.");
    }

    /// <summary>警告メッセージが適切なマークアップで出力されることを検証します。</summary>
    [Fact]
    public void ShowWarningShouldOutputYellowMarkup()
    {
        // Act
        _sut.ShowWarning("Low paper warning.", "WARN");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[WARN] Low paper warning.");
    }

    /// <summary>エラーメッセージが適切なマークアップで出力されることを検証します。</summary>
    [Fact]
    public void ShowErrorShouldOutputRedMarkup()
    {
        // Act
        _sut.ShowError("System crash.", "ERR");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[ERR] System crash.");
    }
}
