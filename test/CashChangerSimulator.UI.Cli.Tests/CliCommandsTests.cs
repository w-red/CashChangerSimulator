using Moq;
using Shouldly;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Models;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Cli.Tests;

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
        var deps = new SimulatorDependencies(); 
        _mockChanger = new Mock<SimulatorCashChanger>(deps);
        
        _mockConsole = new Mock<IAnsiConsole>();
        _mockLocalizer = new Mock<IStringLocalizer>();
        
        _mockDeviceService = new Mock<CliDeviceService>(_mockChanger.Object, _mockConsole.Object, _mockLocalizer.Object);
        _mockCashService = new Mock<CliCashService>(_mockChanger.Object, (Inventory)null!, (Core.Services.ICurrencyMetadataProvider)null!, new CliSessionOptions(), _mockConsole.Object, _mockLocalizer.Object);
        _mockConfigService = new Mock<CliConfigService>((Core.Configuration.ConfigurationProvider)null!, _mockConsole.Object, _mockLocalizer.Object);
        _mockViewService = new Mock<CliViewService>(_mockChanger.Object, (Inventory)null!, (Core.Services.ICurrencyMetadataProvider)null!, (Core.Transactions.TransactionHistory)null!, _mockConsole.Object, _mockLocalizer.Object);
        _mockScriptService = new Mock<CliScriptService>((Device.Services.IScriptExecutionService)null!, _mockConsole.Object, _mockLocalizer.Object);

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

    [Fact]
    public void Open_ShouldInvokeDeviceService()
    {
        _commands.Open();
        _mockDeviceService.Verify(s => s.Open(), Times.Once);
    }

    [Fact]
    public void Claim_ShouldInvokeDeviceServiceWithTimeout()
    {
        _commands.Claim(1234);
        _mockDeviceService.Verify(s => s.Claim(1234), Times.Once);
    }

    [Fact]
    public void Enable_ShouldInvokeDeviceService()
    {
        _commands.Enable();
        _mockDeviceService.Verify(s => s.Enable(), Times.Once);
    }

    [Fact]
    public void Deposit_ShouldInvokeCashService()
    {
        _commands.Deposit(1000);
        _mockCashService.Verify(s => s.Deposit(1000), Times.Once);
    }

    [Fact]
    public void FixDeposit_ShouldInvokeCashService()
    {
        _commands.FixDeposit();
        _mockCashService.Verify(s => s.FixDeposit(), Times.Once);
    }

    [Fact]
    public void Dispense_ShouldInvokeCashService()
    {
        _commands.Dispense(500);
        _mockCashService.Verify(s => s.Dispense(500), Times.Once);
    }

    [Fact]
    public void ConfigList_ShouldInvokeConfigService()
    {
        _commands.ConfigList();
        _mockConfigService.Verify(s => s.List(), Times.Once);
    }

    [Fact]
    public void Status_ShouldInvokeViewService()
    {
        _commands.Status();
        _mockViewService.Verify(s => s.Status(), Times.Once);
    }

    [Fact]
    public async Task RunScript_ShouldInvokeScriptService()
    {
        await _commands.RunScript("test.json");
        _mockScriptService.Verify(s => s.RunScriptAsync("test.json"), Times.Once);
    }
    
    [Fact]
    public void Help_ShouldWriteToConsole()
    {
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));

        _commands.Help();
        
        _mockConsole.Verify(c => c.Write(It.IsAny<Rule>()), Times.Once);
        _mockConsole.Verify(c => c.Write(It.IsAny<Table>()), Times.Once);
    }
}
