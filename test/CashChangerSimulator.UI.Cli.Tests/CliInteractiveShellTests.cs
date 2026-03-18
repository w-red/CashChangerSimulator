using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using Xunit;
using Microsoft.PointOfService;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliInteractiveShell の対話型ループを検証するためのテストクラス。</summary>
public class CliInteractiveShellTests
{
    private readonly Mock<ICliCommandDispatcher> _mockDispatcher;
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly TestConsole _console;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly Mock<ILineReader> _mockReader;
    private readonly CliSessionOptions _options;
    private readonly CliInteractiveShell _shell;

    public CliInteractiveShellTests()
    {
        _mockDispatcher = new Mock<ICliCommandDispatcher>();
        _mockChanger = new Mock<SimulatorCashChanger>(new CashChangerSimulator.Device.Coordination.SimulatorDependencies());
        _console = new TestConsole();
        
        // Ensure interactivity and ANSI support are fully enabled
        _console.Profile.Capabilities.Interactive = true;
        _console.Profile.Capabilities.Ansi = true;
        _console.Profile.Width = 80;
        _console.Profile.Height = 24;
        
        _mockLocalizer = new Mock<IStringLocalizer>();
        _mockReader = new Mock<ILineReader>();
        _options = new CliSessionOptions { IsAsync = false };

        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        _shell = new CliInteractiveShell(
            _mockDispatcher.Object, 
            _mockChanger.Object, 
            _console, 
            _mockLocalizer.Object, 
            _options, 
            _mockReader.Object);
    }

    /// <summary>コマンドが入力された場合、適切にディスパッチされて終了できることを検証します。</summary>
    [Fact]
    public async Task RunAsyncShouldDispatchCommandAndExit()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "status", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        _mockChanger.Setup(c => c.State).Returns(ControlState.Closed);

        // Act
        await _shell.RunAsync();

        // Assert
        _mockDispatcher.Verify(d => d.DispatchAsync("status"), Times.Once);
        _mockChanger.Verify(c => c.Close(), Times.Once);
    }

    /// <summary>空の入力があった場合、コマンド選択メニューが表示されることを検証します。</summary>
    [Fact]
    public async Task RunAsyncEmptyInputShouldTriggerSelectionMenu()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        _mockChanger.Setup(c => c.State).Returns(ControlState.Closed);
        
        // Push Enter to select the first item in the menu ("status").
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _shell.RunAsync();

        // Assert
        _mockDispatcher.Verify(d => d.DispatchAsync("status"), Times.AtLeastOnce);
    }

    /// <summary>menu コマンドが入力された場合、コマンド選択メニューが表示されることを検証します。</summary>
    [Fact]
    public async Task RunAsyncMenuCommandShouldTriggerSelectionMenu()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "menu", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        _mockChanger.Setup(c => c.State).Returns(ControlState.Closed);
        
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _shell.RunAsync();

        // Assert
        _mockDispatcher.Verify(d => d.DispatchAsync(It.IsAny<string>()), Times.AtLeastOnce);
    }

    /// <summary>引数を必要とするコマンドが選択された場合、追加の入力プロンプトが表示されることを検証します。</summary>
    [Fact]
    public async Task RunAsyncSelectionWithArgumentsShouldHandleCorrectly()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        _mockChanger.Setup(c => c.State).Returns(ControlState.Closed);
        
        // Move to 'claim' (Index 7: status, read-counts, deposit, fix-deposit, end-deposit, dispense, open, claim)
        for(int i=0; i<7; i++) _console.Input.PushKey(ConsoleKey.DownArrow);
        _console.Input.PushKey(ConsoleKey.Enter);
        
        // Respond to the TextPrompt<int> for timeout
        _console.Input.PushKey(ConsoleKey.D3);
        _console.Input.PushKey(ConsoleKey.D0);
        _console.Input.PushKey(ConsoleKey.D0);
        _console.Input.PushKey(ConsoleKey.D0);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _shell.RunAsync();

        // Assert
        _mockDispatcher.Verify(d => d.DispatchAsync("claim 3000"), Times.Once);
    }

    /// <summary>終了確認がキャンセルされた場合、ループが継続されることを検証します。</summary>
    [Fact]
    public async Task RunAsyncConfirmExitWhenCancelledShouldContinueLoop()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "exit", "status", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        
        // Device is open, so it triggers confirmation
        _mockChanger.Setup(c => c.State).Returns(ControlState.Idle);
        
        // 1. First "exit": confirm "No" ('n' + Enter)
        _console.Input.PushKey(ConsoleKey.N);
        _console.Input.PushKey(ConsoleKey.Enter);
        
        // 2. Second "exit": confirm "Yes" ('y' + Enter)
        _console.Input.PushKey(ConsoleKey.Y);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _shell.RunAsync();

        // Assert
        _mockDispatcher.Verify(d => d.DispatchAsync("status"), Times.Once);
    }
}
