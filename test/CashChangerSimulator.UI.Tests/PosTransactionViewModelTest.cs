using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Tests;

public class PosTransactionViewModelTest
{
    [Fact]
    public void StartTransactionShouldCallOposSequence()
    {
        // Setup
        var inv = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var hw = new HardwareStatusManager();
        var dep = new DepositController(inv);
        var disp = new DispenseController(manager, null, new Mock<IDeviceSimulator>().Object);
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Inventory.TryAdd("JPY", new CashChangerSimulator.Core.Configuration.InventorySettings());
        configProvider.Config.CurrencyCode = "JPY";

        var monitorsProvider = new MonitorsProvider(inv, configProvider, new CurrencyMetadataProvider(configProvider));
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        
        var cc = new SimulatorCashChanger(configProvider, inv, history, manager, dep, disp, aggregatorProvider, hw);
        cc.SkipStateVerification = true;
        cc.CurrencyCode = "JPY";
        
        var depVm = new Mock<DepositViewModel>(dep, hw, (Func<IEnumerable<DenominationViewModel>>)(() => Enumerable.Empty<DenominationViewModel>()));
        var dispVm = new Mock<DispenseViewModel>(inv, manager, disp, new ConfigurationProvider(), Observable.Return(false), Observable.Return(false), (Func<IEnumerable<DenominationViewModel>>)(() => Enumerable.Empty<DenominationViewModel>()));
        
        var vm = new PosTransactionViewModel(depVm.Object, dispVm.Object, cc);

        vm.TargetAmountInput.Value = "1000";

        // Act
        vm.StartCommand.Execute(Unit.Default);

        // Verify
        vm.TransactionStatus.Value.ShouldBe(PosTransactionStatus.WaitingForCash);
        vm.OposLog.ShouldContain(s => s.Contains("Open()"));
        vm.OposLog.ShouldContain(s => s.Contains("Claim(1000)"));
        vm.OposLog.ShouldContain(s => s.Contains("BeginDeposit()"));
    }

    [Fact]
    public async Task CompleteTransactionShouldCallOposSequence()
    {
        // Setup
        var inv = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var hw = new HardwareStatusManager();
        var dep = new DepositController(inv);
        var disp = new DispenseController(manager, null, new Mock<IDeviceSimulator>().Object);
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Inventory.TryAdd("JPY", new CashChangerSimulator.Core.Configuration.InventorySettings());
        configProvider.Config.CurrencyCode = "JPY";

        var monitorsProvider = new MonitorsProvider(inv, configProvider, new CurrencyMetadataProvider(configProvider));
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        
        var cc = new SimulatorCashChanger(configProvider, inv, history, manager, dep, disp, aggregatorProvider, hw);
        cc.SkipStateVerification = true;
        cc.CurrencyCode = "JPY";
        
        var depVm = new DepositViewModel(dep, hw, () => Enumerable.Empty<DenominationViewModel>());
        var dispVm = new DispenseViewModel(inv, manager, disp, new ConfigurationProvider(), Observable.Return(false), Observable.Return(false), () => Enumerable.Empty<DenominationViewModel>());
        
        var vm = new PosTransactionViewModel(depVm, dispVm, cc);

        vm.TargetAmountInput.Value = "1000";
        // Simulate start
        vm.StartCommand.Execute(Unit.Default);
        vm.OposLog.Clear(); // Clear start logs for easier verification

        // Simulate cash insertion (1500 JPY) at once to avoid race condition
        dep.TrackBulkDeposit(new Dictionary<DenominationKey, int> {
            { new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill), 1 },
            { new DenominationKey(500, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 1 }
        });

        // Act - Trigger completion logic (which is called automatically by subscription)
        // Give it a moment to process the async completion
        await Task.Delay(5000);

        // Verify
        try
        {
            vm.OposLog.ShouldContain(s => s.Contains("FixDeposit()"));
            vm.OposLog.ShouldContain(s => s.Contains("EndDeposit(NoChange)"));
            vm.OposLog.ShouldContain(s => s.Contains("DispenseChange(500)"));
            vm.OposLog.ShouldContain(s => s.Contains("Release()"));
            vm.OposLog.ShouldContain(s => s.Contains("Close()"));
        }
        catch (Exception ex)
        {
            var logs = string.Join("\n", vm.OposLog);
            throw new Exception($"Test Failed. OposLog:\n{logs}\n\nOriginal Exception: {ex.Message}", ex);
        }
    }
}
