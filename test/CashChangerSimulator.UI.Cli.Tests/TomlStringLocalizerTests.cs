using System.Globalization;
using CashChangerSimulator.UI.Cli.Localization;
using Shouldly;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>TomlStringLocalizer のローカライズ機能を検証するためのテストクラス。</summary>
public class TomlStringLocalizerTests : IDisposable
{
    private readonly string _tempPath;

    public TomlStringLocalizerTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), "CliLocalizerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempPath);

        // Create test TOML files
        File.WriteAllText(Path.Combine(_tempPath, "cli.en.toml"), @"
[messages]
welcome = ""Welcome to Simulator""
greet = ""Hello, {0}!""
[errors]
failed = ""Operation failed""
");

        File.WriteAllText(Path.Combine(_tempPath, "cli.ja.toml"), @"
[messages]
welcome = ""シミュレータへようこそ""
greet = ""こんにちは、{0}さん！""
");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempPath))
        {
            Directory.Delete(_tempPath, true);
        }
    }

    /// <summary>デフォルト（英語）のカルチャで文字列が取得できることを検証します。</summary>
    [Fact]
    public void ShouldGetEnglishStringByDefault()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var result = localizer["messages.welcome"];

        // Assert
        result.Value.ShouldBe("Welcome to Simulator");
        result.ResourceNotFound.ShouldBeFalse();
    }

    /// <summary>日本語のカルチャで文字列が取得できることを検証します。</summary>
    [Fact]
    public void ShouldGetJapaneseString()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");

        // Act
        var result = localizer["messages.welcome"];

        // Assert
        result.Value.ShouldBe("シミュレータへようこそ");
    }

    /// <summary>引数を含むメッセージが正しくフォーマットされることを検証します。</summary>
    [Fact]
    public void ShouldFormatArguments()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var result = localizer["messages.greet", "Alice"];

        // Assert
        result.Value.ShouldBe("Hello, Alice!");
    }

    /// <summary>リソースが見つからない場合にキー名そのものが返されることを検証します。</summary>
    [Fact]
    public void ShouldReturnKeyNameIfNotFound()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var result = localizer["non.existent.key"];

        // Assert
        result.Value.ShouldBe("non.existent.key");
        result.ResourceNotFound.ShouldBeTrue();
    }

    /// <summary>GetAllStrings で全てのリソースエントリが取得できることを検証します。</summary>
    [Fact]
    public void ShouldGetAllStrings()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var all = localizer.GetAllStrings(false).ToList();

        // Assert
        all.ShouldContain(s => s.Name == "messages.welcome" && s.Value == "Welcome to Simulator");
        all.ShouldContain(s => s.Name == "errors.failed" && s.Value == "Operation failed");
    }

    /// <summary>言語コードのみが一致する場合（例: ja-JP -> ja）でもフォールバックされることを検証します。</summary>
    [Fact]
    public void ShouldFallbackToLanguageOnly()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");

        // Act
        var result = localizer["messages.welcome"];

        // Assert
        result.Value.ShouldBe("シミュレータへようこそ");
    }

    /// <summary>指定された言語のリソースが存在しない場合に英語にフォールバックされることを検証します。</summary>
    [Fact]
    public void ShouldFallbackToEnglishIfNotFound()
    {
        // Arrange
        var localizer = new TomlStringLocalizer(_tempPath);
        CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

        // Act
        var result = localizer["messages.welcome"];

        // Assert
        result.Value.ShouldBe("Welcome to Simulator");
    }

    /// <summary>TOML ファイルの形式が不正な場合に適切にフォールバックされることを検証します。</summary>
    [Fact]
    public void ShouldReturnKeyIfTomlIsInvalid()
    {
        // Arrange
        var invalidPath = Path.Combine(_tempPath, "invalid");
        Directory.CreateDirectory(invalidPath);
        File.WriteAllText(Path.Combine(invalidPath, "cli.en.toml"), "invalid = toml ["); // Syntax error
        
        var localizer = new TomlStringLocalizer(invalidPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var result = localizer["any.key"];

        // Assert
        result.Value.ShouldBe("any.key");
        result.ResourceNotFound.ShouldBeTrue();
    }

    /// <summary>リソースファイルが一つも存在しない場合にキー名が返されることを検証します。</summary>
    [Fact]
    public void ShouldReturnKeyIfNoFilesExist()
    {
        // Arrange
        var emptyPath = Path.Combine(_tempPath, "empty");
        Directory.CreateDirectory(emptyPath);
        var localizer = new TomlStringLocalizer(emptyPath);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        // Act
        var result = localizer["any.key"];

        // Assert
        result.Value.ShouldBe("any.key");
    }
}
