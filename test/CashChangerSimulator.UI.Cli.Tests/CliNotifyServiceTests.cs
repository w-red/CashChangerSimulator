using Shouldly;
using Spectre.Console.Testing;

namespace CashChangerSimulator.UI.Cli.Tests;

public class CliNotifyServiceTests
{
    private readonly TestConsole _console;
    private readonly CliNotifyService _sut;

    public CliNotifyServiceTests()
    {
        _console = new TestConsole();
        _sut = new CliNotifyService(_console);
    }

    [Fact]
    public void ShowInfo_ShouldOutputBlueMarkup()
    {
        // Act
        _sut.ShowInfo("System initialized.", "INFO");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[INFO] System initialized.");
    }

    [Fact]
    public void ShowWarning_ShouldOutputYellowMarkup()
    {
        // Act
        _sut.ShowWarning("Low paper warning.", "WARN");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[WARN] Low paper warning.");
    }

    [Fact]
    public void ShowError_ShouldOutputRedMarkup()
    {
        // Act
        _sut.ShowError("System crash.", "ERR");

        // Assert
        var output = _console.Output;
        output.ShouldContain("[ERR] System crash.");
    }
}
