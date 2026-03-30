using Shouldly;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CLI のオートコンプリート機能を検証するテストクラス。</summary>
public class CliAutoCompleteHandlerTests
{
    private readonly string[] _commands = ["open", "claim", "enable", "deposit", "dispense", "close"];

    /// <summary>空の入力に対して全コマンドを提案することを検証します。</summary>
    [Fact]
    public void GetSuggestionsWithEmptyTextShouldReturnAllCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("", 0);

        // Assert
        suggestions.ShouldBe(_commands);
    }

    /// <summary>前方一致のプリフィックスに対して一致するコマンドを提案することを検証します。</summary>
    [Fact]
    public void GetSuggestionsWithPrefixShouldReturnMatchingCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("clo", 0);

        // Assert
        suggestions.Length.ShouldBe(1);
        suggestions.ShouldContain("close");
    }

    /// <summary>大文字小文字を区別せず、プリフィックスに対して一致するコマンドを提案することを検証します。</summary>
    [Fact]
    public void GetSuggestionsWithPrefixCaseInsensitiveShouldReturnMatchingCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("CLO", 0);

        // Assert
        suggestions.Length.ShouldBe(1);
        suggestions.ShouldContain("close");
    }

    /// <summary>複数のコマンドがマッチする場合に全候補を提案することを検証します。</summary>
    [Fact]
    public void GetSuggestionsWithMultipleMatchesShouldReturnAll()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(["test1", "test2", "other"]);

        // Act
        var suggestions = handler.GetSuggestions("te", 0);

        // Assert
        suggestions.Length.ShouldBe(2);
        suggestions.ShouldContain("test1");
        suggestions.ShouldContain("test2");
    }

    /// <summary>マッチするコマンドがない場合に空のリストを返すことを検証します。</summary>
    [Fact]
    public void GetSuggestionsWithNoMatchesShouldReturnEmpty()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("xyz", 0);

        // Assert
        suggestions.ShouldBeEmpty();
    }
}
