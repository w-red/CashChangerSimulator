using Shouldly;
using System.Globalization;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>Program クラスのグローバルオプション解析機能を検証するためのテストクラス。</summary>
public class ProgramTests
{
    /// <summary>グローバルオプションとコマンド引数が正しく分離されることを検証します。</summary>
    [Fact]
    public void ExtractGlobalOptionsShouldSeparateGlobalsAndCommands()
    {
        // Arrange
        string[] args = ["--async", "deposit", "--lang", "en-US", "1000", "--currency", "USD"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBe(["--async", "--lang", "en-US", "--currency", "USD"]);
        commands.ShouldBe(["deposit", "1000"]);
    }

    /// <summary>グローバルオプションが存在しない場合に全ての引数がコマンドとして扱われることを検証します。</summary>
    [Fact]
    public void ExtractGlobalOptionsNoGlobalsShouldReturnAllCommands()
    {
        // Arrange
        string[] args = ["deposit", "1000"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBeEmpty();
        commands.ShouldBe(["deposit", "1000"]);
    }

    /// <summary>グローバルオプションがセッション設定に正しく反映されることを検証します。</summary>
    [Fact]
    public void ApplyGlobalOptionsShouldUpdateOptions()
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

    /// <summary>一部のグローバルオプションのみが指定された場合に、その他の設定が維持されることを検証します。</summary>
    [Fact]
    public void ApplyGlobalOptionsPartialOptionsShouldOnlyUpdateSpecified()
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

    /// <summary>verbose オプションが正しく分離されることを検証します。</summary>
    [Fact]
    public void ExtractGlobalOptionsWithVerboseShouldSeparateGlobals()
    {
        // Arrange
        string[] args = ["--verbose", "status"];

        // Act
        var (globals, commands) = Program.ExtractGlobalOptions(args);

        // Assert
        globals.ShouldBe(["--verbose"]);
        commands.ShouldBe(["status"]);
    }

    /// <summary>verbose オプションと無効な言語コードが指定された場合の挙動を検証します。</summary>
    [Fact]
    public void ApplyGlobalOptionsWithVerboseAndInvalidLangShouldHandleGracefully()
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

    /// <summary>ヘルプコマンドが正常に動作し、例外を投げずに終了することを検証します。</summary>
    [Fact]
    public void MainHelpCommandShouldExecuteCoconaAndExit()
    {
        // Arrange
        string[] args = ["--help"];

        // Act & Assert
        // This will successfully print the help message and exit without throwing.
        Should.NotThrow(() => Program.Main(args));
    }

    /// <summary>無効なカルチャが指定された場合に安全にフォールバックされることを検証します。</summary>
    [Fact]
    public void ApplyGlobalOptionsInvalidCultureShouldFallbackSafely()
    {
        // Arrange
        var options = new CliSessionOptions { Language = "en-US" };
        string[] globals = ["--lang", "completely-invalid-culture"];

        // Act
        Program.ApplyGlobalOptions(globals, options);

        // Assert
        options.Language.ShouldBe("completely-invalid-culture");
        // Should not throw, and current culture should remain or fallback to something valid
    }
}
