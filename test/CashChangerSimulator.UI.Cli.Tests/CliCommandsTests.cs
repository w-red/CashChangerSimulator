using Moq;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Rendering;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using Xunit;
using System.Threading.Tasks;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliCommands のコマンド実行機能を検証するためのテストクラス。</summary>
public class CliCommandsTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<CliDeviceService> _mockDeviceService;
    private readonly Mock<CliCashService> _mockCashService;
    private readonly Mock<CliConfigService> _mockConfigService;
    private readonly Mock<CliViewService> _mockViewService;
    private readonly Mock<CliScriptService> _mockScriptService;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliCommands _commands;

    public CliCommandsTests()
    {
        _mockChanger = new Mock<SimulatorCashChanger>(new CashChangerSimulator.Device.Coordination.SimulatorDependencies());
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();
        
        // Mocking services that CliCommands depends on
        _mockDeviceService = new Mock<CliDeviceService>(null!, null!, null!);
        _mockCashService = new Mock<CliCashService>(null!, null!, null!, null!, null!, null!);
        _mockConfigService = new Mock<CliConfigService>(null!, null!, null!);
        _mockViewService = new Mock<CliViewService>(null!, null!, null!, null!, null!, null!);
        _mockScriptService = new Mock<CliScriptService>(null!, null!, null!);

        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => new LocalizedString(s, s));

        _commands = new CliCommands(
            _mockChanger.Object,
            _mockDeviceService.Object,
            _mockCashService.Object,
            _mockConfigService.Object,
            _mockViewService.Object,
            _mockScriptService.Object,
            _mockConsole.Object,
            _mockLocalizer.Object);
    }

    /// <summary>Status コマンドが ViewService.Status を呼び出すことを検証します。</summary>
    [Fact]
    public void StatusShouldCallViewService()
    {
        // Act
        _commands.Status();

        // Assert
        _mockViewService.Verify(s => s.Status(), Times.Once);
    }

    /// <summary>Open コマンドが DeviceService.Open を呼び出すことを検証します。</summary>
    [Fact]
    public void OpenShouldCallDeviceService()
    {
        // Act
        _commands.Open();

        // Assert
        _mockDeviceService.Verify(s => s.Open(), Times.Once);
    }

    /// <summary>LogLevel コマンドが有効な入力を受け入れ、成功メッセージを表示することを検証します。</summary>
    [Fact]
    public void LogLevelShouldUpdateOnValidInput()
    {
        // Act
        _commands.LogLevel("Information");

        // Assert
        _mockConsole.Verify(c => c.Write(It.IsAny<IRenderable>()), Times.AtLeastOnce);
    }

    /// <summary>LogLevel コマンドが無効な入力に対してエラーメッセージを表示することを検証します。</summary>
    [Fact]
    public void LogLevelShouldShowErrorOnInvalidInput()
    {
        // Act
        _commands.LogLevel("InvalidLevel");

        // Assert
        _mockLocalizer.Verify(l => l["messages.invalid_log_level", "InvalidLevel"], Times.Once);
    }

    /// <summary>Help コマンドがルールとテーブルをコンソールに出力することを検証します。</summary>
    [Fact]
    public void HelpShouldWriteToConsole()
    {
        // Act
        _commands.Help();

        // Assert
        _mockConsole.Verify(c => c.Write(It.IsAny<Rule>()), Times.Once);
        _mockConsole.Verify(c => c.Write(It.IsAny<Table>()), Times.Once);
    }
}
