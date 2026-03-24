using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CliDeviceService の各種デバイス操作を検証するためのテストクラス。</summary>
public class CliDeviceServiceTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly IAnsiConsole _console;
    private readonly StringWriter _consoleOutput;
    private readonly Mock<IStringLocalizer> _mockLocalizer;
    private readonly CliDeviceService _service;

    public CliDeviceServiceTests()
    {
        _mockChanger = new Mock<SimulatorCashChanger>(new CashChangerSimulator.Device.Coordination.SimulatorDependencies());
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

        _service = new CliDeviceService(_mockChanger.Object, _console, _mockLocalizer.Object);
    }

    /// <summary>Open 操作が成功し、成功メッセージが表示されることを検証します。</summary>
    [Fact]
    public void OpenShouldInvokeChangerOpenAndShowSuccess()
    {
        // Act
        _service.Open();

        // Assert
        _mockChanger.Verify(c => c.Open(), Times.Once);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>Open 操作で例外が発生した場合、エラーハンドリングが行われることを検証します。</summary>
    [Fact]
    public void OpenShouldHandleExceptionWhenChangerFails()
    {
        // Arrange
        _mockChanger.Setup(c => c.Open()).Throws(new PosControlException("Open Failed", ErrorCode.NoHardware));

        // Act
        _service.Open();

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_label");
        _consoleOutput.ToString().ShouldContain("NoHardware");
    }

    /// <summary>Claim 操作が指定されたタイムアウト値で実行されることを検証します。</summary>
    [Fact]
    public void ClaimShouldInvokeChangerClaimWithTimeout()
    {
        // Act
        _service.Claim(5000);

        // Assert
        _mockChanger.Verify(c => c.Claim(5000), Times.Once);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>Enable 操作が成功することを検証します。</summary>
    [Fact]
    public void EnableShouldSetDeviceEnabledToTrue()
    {
        // Act
        _service.Enable();

        // Assert
        _mockChanger.VerifySet(c => c.DeviceEnabled = true);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>Disable 操作が成功することを検証します。</summary>
    [Fact]
    public void DisableShouldSetDeviceEnabledToFalse()
    {
        // Act
        _service.Disable();

        // Assert
        _mockChanger.VerifySet(c => c.DeviceEnabled = false);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>Release 操作が成功することを検証します。</summary>
    [Fact]
    public void ReleaseShouldInvokeChangerRelease()
    {
        // Act
        _service.Release();

        // Assert
        _mockChanger.Verify(c => c.Release(), Times.Once);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>Close 操作が成功することを検証します。</summary>
    [Fact]
    public void CloseShouldInvokeChangerClose()
    {
        // Act
        _service.Close();

        // Assert
        _mockChanger.Verify(c => c.Close(), Times.Once);
        _consoleOutput.ToString().ShouldContain("messages.success_label");
    }

    /// <summary>GetSummary で金庫満杯時のカスタムサマリが返されることを検証します。</summary>
    [Fact]
    public void GetSummaryShouldReturnCustomMessageForExtendedErrorFull()
    {
        // Arrange
        var ex = new PosControlException("Full", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Full);

        // Act
        _service.HandleException(ex);

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_summary_full");
    }

    /// <summary>GetSummary で金庫空時のカスタムサマリが返されることを検証します。</summary>
    [Fact]
    public void GetSummaryShouldReturnCustomMessageForExtendedErrorEmpty()
    {
        // Arrange
        var ex = new PosControlException("Empty", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Empty);

        // Act
        _service.HandleException(ex);

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_summary_empty");
    }

    /// <summary>GetSummary でタイムアウト時のサマリが返されることを検証します。</summary>
    [Fact]
    public void GetSummaryShouldReturnTimeoutMessage()
    {
        // Arrange
        var ex = new PosControlException("Timeout", ErrorCode.Timeout);

        // Act
        _service.HandleException(ex);

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_summary_timeout");
    }

    /// <summary>GetSummary でリソースが見つからない場合に汎用メッセージが返されることを検証します。</summary>
    [Fact]
    public void GetSummaryShouldReturnGenericMessageWhenResourceNotFound()
    {
        // Arrange
        var ex = new PosControlException("Failure", ErrorCode.Failure);
        _mockLocalizer.Setup(l => l["messages.error_summary_failure"]).Returns(new LocalizedString("messages.error_summary_failure", "Summary", true));

        // Act
        _service.HandleException(ex);

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_summary_generic");
    }

    /// <summary>GetHint でリソースが見つからない場合に汎用メッセージが返されることを検証します。</summary>
    [Fact]
    public void GetHintShouldReturnGenericMessageWhenResourceNotFound()
    {
        // Arrange
        var ex = new PosControlException("Failure", ErrorCode.Failure);
        _mockLocalizer.Setup(l => l["messages.error_hint_failure"]).Returns(new LocalizedString("messages.error_hint_failure", "Hint", true));

        // Act
        _service.HandleException(ex);

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_hint_generic");
    }

    /// <summary>各メソッドで例外が発生した場合に HandleException が呼ばれることを検証するための理論テスト。</summary>
    [Theory]
    [InlineData("Open")]
    [InlineData("Claim")]
    [InlineData("Enable")]
    [InlineData("Disable")]
    [InlineData("Release")]
    [InlineData("Close")]
    public void MethodsShouldHandleExceptions(string methodName)
    {
        // Arrange
        var exception = new PosControlException("Error", ErrorCode.Failure);
        switch (methodName)
        {
            case "Open": _mockChanger.Setup(c => c.Open()).Throws(exception); break;
            case "Claim": _mockChanger.Setup(c => c.Claim(It.IsAny<int>())).Throws(exception); break;
            case "Enable": _mockChanger.SetupSet(c => c.DeviceEnabled = true).Throws(exception); break;
            case "Disable": _mockChanger.SetupSet(c => c.DeviceEnabled = false).Throws(exception); break;
            case "Release": _mockChanger.Setup(c => c.Release()).Throws(exception); break;
            case "Close": _mockChanger.Setup(c => c.Close()).Throws(exception); break;
        }

        // Act
        switch (methodName)
        {
            case "Open": _service.Open(); break;
            case "Claim": _service.Claim(1000); break;
            case "Enable": _service.Enable(); break;
            case "Disable": _service.Disable(); break;
            case "Release": _service.Release(); break;
            case "Close": _service.Close(); break;
        }

        // Assert
        _consoleOutput.ToString().ShouldContain("messages.error_label");
    }
}
