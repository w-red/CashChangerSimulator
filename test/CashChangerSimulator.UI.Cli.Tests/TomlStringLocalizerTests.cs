using System.Globalization;
using System.IO;
using CashChangerSimulator.UI.Cli.Localization;
using Microsoft.Extensions.Localization;
using Xunit;
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

    /// <summary>英語（デフォルト）の文字列が正しく取得されることを検証します。</summary>
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

    /// <summary>日本語の文字列が正しく取得されることを検証します。</summary>
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

    /// <summary>引数を持つ文字列が正しくフォーマットされることを検証します。</summary>
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

    /// <summary>キーが見つからない場合にキー名が返されることを検証します。</summary>
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

    /// <summary>全ての文字列を取得できることを検証します。</summary>
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
}
