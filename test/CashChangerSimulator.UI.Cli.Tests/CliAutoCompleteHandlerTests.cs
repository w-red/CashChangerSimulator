using Shouldly;

namespace CashChangerSimulator.UI.Cli.Tests;

public class CliAutoCompleteHandlerTests
{
    private readonly string[] _commands = ["open", "claim", "enable", "deposit", "dispense", "close"];

    [Fact]
    public void GetSuggestions_WithEmptyText_ShouldReturnAllCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("", 0);

        // Assert
        suggestions.ShouldBe(_commands);
    }

    [Fact]
    public void GetSuggestions_WithPrefix_ShouldReturnMatchingCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("clo", 0);

        // Assert
        suggestions.Length.ShouldBe(1);
        suggestions.ShouldContain("close");
    }

    [Fact]
    public void GetSuggestions_WithPrefixCaseInsensitive_ShouldReturnMatchingCommands()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("CLO", 0);

        // Assert
        suggestions.Length.ShouldBe(1);
        suggestions.ShouldContain("close");
    }

    [Fact]
    public void GetSuggestions_WithMultipleMatches_ShouldReturnAll()
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

    [Fact]
    public void GetSuggestions_WithNoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var handler = new CliAutoCompleteHandler(_commands);

        // Act
        var suggestions = handler.GetSuggestions("xyz", 0);

        // Assert
        suggestions.ShouldBeEmpty();
    }
}
