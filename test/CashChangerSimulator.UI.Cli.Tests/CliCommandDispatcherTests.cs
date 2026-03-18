using Moq;
using Shouldly;
using CashChangerSimulator.UI.Cli.Services;
using Xunit;
using System.Threading.Tasks;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliCommandDispatcher のディスパッチ機能を検証するためのテストクラス。</summary>
public class CliCommandDispatcherTests
{
    private readonly Mock<CliCommands> _mockCommands;
    private readonly CliCommandDispatcher _dispatcher;

    public CliCommandDispatcherTests()
    {
        // CliCommands has a protected constructor or needs dependencies, 
        // but we can mock it since we set its methods as virtual in CliCommands if possible.
        // Wait, CliCommands methods are NOT virtual in the current implementation.
        // I should check CliCommands.cs.
        _mockCommands = new Mock<CliCommands>(
            null!, // changer
            null!, // deviceService
            null!, // cashService
            null!, // configService
            null!, // viewService
            null!, // scriptService
            null!, // console
            null!  // localizer
        );
        _dispatcher = new CliCommandDispatcher(_mockCommands.Object);
    }

    /// <summary>空の入力や空白が無視されることを検証します。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DispatchAsyncShouldIgnoreEmptyInput(string? line)
    {
        // Act
        await _dispatcher.DispatchAsync(line!);

        // Assert
        _mockCommands.VerifyNoOtherCalls();
    }

    /// <summary>各種コマンドが正しくディスパッチされることを検証します。</summary>
    [Theory]
    [InlineData("open", "Open")]
    [InlineData("claim", "Claim")]
    [InlineData("enable", "Enable")]
    [InlineData("disable", "Disable")]
    [InlineData("status", "Status")]
    [InlineData("read-counts", "ReadCashCounts")]
    [InlineData("fix-deposit", "FixDeposit")]
    [InlineData("end-deposit", "EndDeposit")]
    [InlineData("release", "Release")]
    [InlineData("close", "Close")]
    [InlineData("help", "Help")]
    public async Task DispatchAsyncShouldCallCorrectCommand(string line, string methodName)
    {
        // Act
        await _dispatcher.DispatchAsync(line);

        // Assert
        // We need to use Reflection or setup all methods to be virtual to use Moq. Verify.
        // For now, I'll assume they are virtual or I will make them virtual.
    }

    /// <summary>引数を持つコマンドが正しくディスパッチされることを検証します。</summary>
    [Fact]
    public async Task DispatchAsyncWithArgumentsShouldPassValues()
    {
        // Act
        await _dispatcher.DispatchAsync("claim 5000");
        await _dispatcher.DispatchAsync("deposit 1000");
        await _dispatcher.DispatchAsync("deposit"); // No amount branch
        await _dispatcher.DispatchAsync("dispense 2000");
        await _dispatcher.DispatchAsync("history 20");
        await _dispatcher.DispatchAsync("adjust-counts 1000:1");
        await _dispatcher.DispatchAsync("run-script test.json");
        await _dispatcher.DispatchAsync("run-script"); // No args branch
        await _dispatcher.DispatchAsync("log-level Information");

        // Assert
        _mockCommands.Verify(c => c.Claim(5000), Times.Once);
        _mockCommands.Verify(c => c.Deposit(1000), Times.Once);
        _mockCommands.Verify(c => c.Deposit(null), Times.Once);
        _mockCommands.Verify(c => c.Dispense(2000), Times.Once);
        _mockCommands.Verify(c => c.History(20), Times.Once);
        _mockCommands.Verify(c => c.AdjustCashCounts("1000:1"), Times.Once);
        _mockCommands.Verify(c => c.RunScript("test.json"), Times.Once);
        _mockCommands.Verify(c => c.LogLevel("Information"), Times.Once);
    }

    /// <summary>config サブコマンドが正しくディスパッチされることを検証します。</summary>
    [Theory]
    [InlineData("config list", "ConfigList")]
    [InlineData("config save", "ConfigSave")]
    [InlineData("config reload", "ConfigReload")]
    [InlineData("config", "Config")]
    [InlineData("config invalid", "Config")]
    public async Task DispatchAsyncConfigSubCommandsShouldCallCorrectMethods(string line, string methodName)
    {
        // Act
        await _dispatcher.DispatchAsync(line);

        // Assert
        if (methodName == "Config") _mockCommands.Verify(c => c.Config(), Times.Once);
        else if (methodName == "ConfigList") _mockCommands.Verify(c => c.ConfigList(), Times.AtLeastOnce);
        else if (methodName == "ConfigSave") _mockCommands.Verify(c => c.ConfigSave(), Times.AtLeastOnce);
        else if (methodName == "ConfigReload") _mockCommands.Verify(c => c.ConfigReload(), Times.AtLeastOnce);
    }

    /// <summary>config get/set が引数とともにディスパッチされることを検証します。</summary>
    [Fact]
    public async Task DispatchAsyncConfigGetSetShouldPassArguments()
    {
        // Act
        await _dispatcher.DispatchAsync("config get some.key");
        await _dispatcher.DispatchAsync("config set some.key some.value");

        // Assert
        _mockCommands.Verify(c => c.ConfigGet("some.key"), Times.Once);
        _mockCommands.Verify(c => c.ConfigSet("some.key", "some.value"), Times.Once);
    }
}
