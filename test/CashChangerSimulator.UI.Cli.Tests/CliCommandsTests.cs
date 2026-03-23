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
        _mockViewService = new Mock<CliViewService>(null!, null!, null!, null!, null!, null!, null!);
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

    /// <summary>Claim コマンドが DeviceService.Claim を呼び出すことを検証します。</summary>
    [Fact]
    public void ClaimShouldCallDeviceService()
    {
        _commands.Claim(1000);
        _mockDeviceService.Verify(s => s.Claim(1000), Times.Once);
    }

    /// <summary>Enable コマンドが DeviceService.Enable を呼び出すことを検証します。</summary>
    [Fact]
    public void EnableShouldCallDeviceService()
    {
        _commands.Enable();
        _mockDeviceService.Verify(s => s.Enable(), Times.Once);
    }

    /// <summary>Disable コマンドが DeviceService.Disable を呼び出すことを検証します。</summary>
    [Fact]
    public void DisableShouldCallDeviceService()
    {
        _commands.Disable();
        _mockDeviceService.Verify(s => s.Disable(), Times.Once);
    }

    /// <summary>Release コマンドが DeviceService.Release を呼び出すことを検証します。</summary>
    [Fact]
    public void ReleaseShouldCallDeviceService()
    {
        _commands.Release();
        _mockDeviceService.Verify(s => s.Release(), Times.Once);
    }

    /// <summary>Close コマンドが DeviceService.Close を呼び出すことを検証します。</summary>
    [Fact]
    public void CloseShouldCallDeviceService()
    {
        _commands.Close();
        _mockDeviceService.Verify(s => s.Close(), Times.Once);
    }

    /// <summary>ReadCashCounts コマンドが CashService.ReadCashCounts を呼び出すことを検証します。</summary>
    [Fact]
    public void ReadCashCountsShouldCallCashService()
    {
        _commands.ReadCashCounts();
        _mockCashService.Verify(s => s.ReadCashCounts(), Times.Once);
    }

    /// <summary>Deposit コマンドが CashService.Deposit を呼び出すことを検証します。</summary>
    [Fact]
    public void DepositShouldCallCashService()
    {
        _commands.Deposit(1000);
        _mockCashService.Verify(s => s.Deposit(1000), Times.Once);
    }

    /// <summary>FixDeposit コマンドが CashService.FixDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void FixDepositShouldCallCashService()
    {
        _commands.FixDeposit();
        _mockCashService.Verify(s => s.FixDeposit(), Times.Once);
    }

    /// <summary>EndDeposit コマンドが CashService.EndDeposit を呼び出すことを検証します。</summary>
    [Fact]
    public void EndDepositShouldCallCashService()
    {
        _commands.EndDeposit();
        _mockCashService.Verify(s => s.EndDeposit(), Times.Once);
    }

    /// <summary>Dispense コマンドが CashService.Dispense を呼び出すことを検証します。</summary>
    [Fact]
    public void DispenseShouldCallCashService()
    {
        _commands.Dispense(1000);
        _mockCashService.Verify(s => s.Dispense(1000), Times.Once);
    }

    /// <summary>AdjustCashCounts コマンドが CashService.AdjustCashCounts を呼び出すことを検証します。</summary>
    [Fact]
    public void AdjustCashCountsShouldCallCashService()
    {
        _commands.AdjustCashCounts("1000:1");
        _mockCashService.Verify(s => s.AdjustCashCounts("1000:1"), Times.Once);
    }

    /// <summary>History コマンドが ViewService.History を呼び出すことを検証します。</summary>
    [Fact]
    public void HistoryShouldCallViewService()
    {
        _commands.History(10);
        _mockViewService.Verify(s => s.History(10), Times.Once);
    }

    /// <summary>ExportHistory コマンドが ViewService.ExportHistory を呼び出すことを検証します。</summary>
    [Fact]
    public void ExportHistoryShouldCallViewService()
    {
        _commands.ExportHistory("test.csv");
        _mockViewService.Verify(s => s.ExportHistory("test.csv"), Times.Once);
    }

    /// <summary>Run Script コマンドが ScriptService.RunScriptAsync を呼び出すことを検証します。</summary>
    [Fact]
    public async Task RunScriptShouldCallScriptService()
    {
        await _commands.RunScript("test.json");
        _mockScriptService.Verify(s => s.RunScriptAsync("test.json"), Times.Once);
    }


    /// <summary>ConfigList コマンドが ConfigService.List を呼び出すことを検証します。</summary>
    [Fact]
    public void ConfigListShouldCallConfigService()
    {
        _commands.ConfigList();
        _mockConfigService.Verify(s => s.List(), Times.Once);
    }

    /// <summary>ConfigGet コマンドが ConfigService.Get を呼び出すことを検証します。</summary>
    [Fact]
    public void ConfigGetShouldCallConfigService()
    {
        _commands.ConfigGet("key");
        _mockConfigService.Verify(s => s.Get("key"), Times.Once);
    }

    /// <summary>ConfigSet コマンドが ConfigService.Set を呼び出すことを検証します。</summary>
    [Fact]
    public void ConfigSetShouldCallConfigService()
    {
        _commands.ConfigSet("key", "value");
        _mockConfigService.Verify(s => s.Set("key", "value"), Times.Once);
    }

    /// <summary>ConfigSave コマンドが ConfigService.Save を呼び出すことを検証します。</summary>
    [Fact]
    public void ConfigSaveShouldCallConfigService()
    {
        _commands.ConfigSave();
        _mockConfigService.Verify(s => s.Save(), Times.Once);
    }

    /// <summary>ConfigReload コマンドが ConfigService.Reload を呼び出すことを検証します。</summary>
    [Fact]
    public void ConfigReloadShouldCallConfigService()
    {
        _commands.ConfigReload();
        _mockConfigService.Verify(s => s.Reload(), Times.Once);
    }

    /// <summary>HandleAsyncError がコンソールに出力することを検証します。</summary>
    [Fact]
    public void HandleAsyncErrorShouldWriteToConsole()
    {
        var args = new Microsoft.PointOfService.DeviceErrorEventArgs(Microsoft.PointOfService.ErrorCode.Failure, 0, Microsoft.PointOfService.ErrorLocus.Output, Microsoft.PointOfService.ErrorResponse.Clear);
        _commands.HandleAsyncError(null!, args);
        _mockConsole.Verify(c => c.Write(It.IsAny<IRenderable>()), Times.AtLeastOnce);
    }
}
