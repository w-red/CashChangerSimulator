using Shouldly;
using System.Globalization;
using CashChangerSimulator.UI.Cli.Services;

namespace CashChangerSimulator.UI.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void ExtractGlobalOptions_ShouldSeparateGlobalsAndCommands()
    {
        // Arrange
        string[] args = ["--async", "deposit", "--lang", "en-US", "1000", "--currency", "USD"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBe(["--async", "--lang", "en-US", "--currency", "USD"]);
        commands.ShouldBe(["deposit", "1000"]);
    }

    [Fact]
    public void ExtractGlobalOptions_NoGlobals_ShouldReturnAllCommands()
    {
        // Arrange
        string[] args = ["deposit", "1000"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBeEmpty();
        commands.ShouldBe(["deposit", "1000"]);
    }

    [Fact]
    public void ApplyGlobalOptions_ShouldUpdateOptions()
    {
        // Arrange
        var options = new CliSessionOptions();
        string[] globals = ["--async", "--lang", "ja-JP", "--currency", "JPY"];

        // Act
        Program.ApplyGlobalOptions(globals, options);

        // Assert
        options.IsAsync.ShouldBeTrue();
        options.Language.ShouldBe("ja-JP");
        options.CurrencyCode.ShouldBe("JPY");
        CultureInfo.DefaultThreadCurrentCulture?.Name.ShouldBe("ja-JP");
    }

    [Fact]
    public void ApplyGlobalOptions_PartialOptions_ShouldOnlyUpdateSpecified()
    {
        // Arrange
        var options = new CliSessionOptions { IsAsync = false, Language = "en-US", CurrencyCode = "USD" };
        string[] globals = ["--async"];

        // Act
        Program.ApplyGlobalOptions(globals, options);

        // Assert
        options.IsAsync.ShouldBeTrue();
        options.Language.ShouldBe("en-US");
        options.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void ExtractGlobalOptions_WithVerbose_ShouldSeparateGlobals()
    {
        // Arrange
        string[] args = ["--verbose", "status"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBe(["--verbose"]);
        commands.ShouldBe(["status"]);
    }

    [Fact]
    public void ApplyGlobalOptions_WithVerboseAndInvalidLang_ShouldHandleGracefully()
    {
        // Arrange
        var options = new CliSessionOptions();
        string[] globals = ["--verbose", "--lang", "invalid-lang-code"];

        // Act
        Program.ApplyGlobalOptions(globals, options);

        // Assert
        options.Verbose.ShouldBeTrue();
        options.Language.ShouldBe("invalid-lang-code");
        // Invalid language gracefully handled by empty catch blocks
    }

    [Fact]
    public void Main_HelpCommand_ShouldExecuteCoconaAndExit()
    {
        // Arrange
        string[] args = ["--help"];

        // Act & Assert
        // This will successfully print the help message and exit without throwing.
        Should.NotThrow(() => Program.Main(args));
    }
}
