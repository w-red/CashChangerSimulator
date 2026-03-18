using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core.Configuration;
using Xunit;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliConfigService の設定操作を検証するためのテストクラス。</summary>
public class CliConfigServiceTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly IAnsiConsole _console;
    private readonly StringWriter _consoleOutput;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliConfigService _service;

    public CliConfigServiceTests()
    {
        _configProvider = new ConfigurationProvider();
        var config = new SimulatorConfiguration
        {
            System = new SystemSettings { CultureCode = "en-US" },
            Logging = new LoggingSettings { LogLevel = "Information" },
            Simulation = new SimulationSettings { DispenseDelayMs = 500 }
        };
        _configProvider.Update(config);

        _consoleOutput = new StringWriter();
        _console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(_consoleOutput)
        });
        _mockLocalizer = new Mock<IStringLocalizer>();

        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args == null || args.Length == 0 ? s : $"{s}({string.Join(", ", args)})"));

        _service = new CliConfigService(_configProvider, _console, _mockLocalizer.Object);
    }

    /// <summary>List 操作で設定プロパティの一覧が表示されることを検証します。</summary>
    [Fact]
    public void ListShouldPrintConfigProperties()
    {
        // Act
        _service.List();

        // Assert
        var output = _consoleOutput.ToString();
        output.ShouldContain("Logging.LogLevel = Information");
        output.ShouldContain("Simulation.DispenseDelayMs = 500");
    }

    /// <summary>有効なキーを指定した場合、その値が表示されることを検証します。</summary>
    [Fact]
    public void GetValidKeyShouldPrintValue()
    {
        // Act
        _service.Get("Logging.LogLevel");

        // Assert
        _consoleOutput.ToString().ShouldContain("Logging.LogLevel = Information");
    }

    /// <summary>無効なキーを指定した場合、エラーメッセージが表示されることを検証します。</summary>
    [Fact]
    public void GetInvalidKeyShouldPrintErrorMessage()
    {
        // Act
        _service.Get("Invalid.Key");

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.invalid_config_key(Invalid.Key)");
    }

    /// <summary>有効なキーと値を指定した場合、設定が更新され成功が報告されることを検証します。</summary>
    [Fact]
    public void SetValidKeyShouldUpdateConfigAndReportSuccess()
    {
        // Act
        _service.Set("Logging.LogLevel", "Debug");

        // Assert
        _configProvider.Config.Logging.LogLevel.ShouldBe("Debug");
        _consoleOutput.ToString().ShouldContain("messages.success_label");
        _consoleOutput.ToString().ShouldContain("messages.config_updated(Logging.LogLevel, Debug)");
    }

    /// <summary>数値型のキーに対して正しい値が設定されることを検証します。</summary>
    [Fact]
    public void SetIntKeyShouldUpdateConfigCorrectly()
    {
        // Act
        _service.Set("Simulation.DispenseDelayMs", "1000");

        // Assert
        _configProvider.Config.Simulation.DispenseDelayMs.ShouldBe(1000);
    }

    /// <summary>無効な型の値を指定した場合、設定が更新されずエラーメッセージが表示されることを検証します。</summary>
    [Fact]
    public void SetInvalidValueTypeShouldReturnFalseAndNotUpdate()
    {
        // Act
        _service.Set("Simulation.DispenseDelayMs", "not-a-number");

        // Assert
        _configProvider.Config.Simulation.DispenseDelayMs.ShouldBe(500);
        _consoleOutput.ToString().ShouldContain("messages.invalid_config_key(Simulation.DispenseDelayMs)");
    }

    /// <summary>Save 操作が成功し、成功メッセージが表示されることを検証します。</summary>
    [Fact]
    public void SaveShouldReportSuccess()
    {
        // Act
        _service.Save();

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.success_label");
        _consoleOutput.ToString().ShouldContain("messages.config_saved");
    }

    /// <summary>Reload 操作が成功し、成功メッセージが表示されることを検証します。</summary>
    [Fact]
    public void ReloadShouldReportSuccess()
    {
        // Act
        _service.Reload();

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.success_label");
        _consoleOutput.ToString().ShouldContain("messages.config_reloaded");
    }
}
