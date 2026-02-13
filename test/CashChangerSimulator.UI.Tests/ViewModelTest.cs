using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Xunit;

namespace CashChangerSimulator.UI.Tests;

public class ViewModelTest
{
    [Fact]
    public void MainViewModel_Dispense_ShouldCallManagerAndClearInput()
    {
        // Setup
        var mockInventory = new Mock<Inventory>();
        mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<int>());
        mockInventory.Setup(i => i.CalculateTotal()).Returns(0m);

        var mockHistory = new Mock<TransactionHistory>();
        mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        // Managerのメソッドがvirtualなのでモック可能です
        var mockManager = new Mock<CashChangerManager>(mockInventory.Object, mockHistory.Object);
        
        // プロバイダー系は依存関係を注入した実インスタンスを使用するのが簡単です
        var realConfig = new ConfigurationProvider();
        var realMonitors = new MonitorsProvider(mockInventory.Object, realConfig);
        var realAggregator = new OverallStatusAggregatorProvider(realMonitors);
        
        var vm = new MainViewModel(
            mockInventory.Object, 
            mockHistory.Object, 
            mockManager.Object, 
            realMonitors, 
            realAggregator);
        
        // Dispense 1000
        vm.DispenseAmountInput = "1000";
        vm.DispenseCommand.Execute(Unit.Default);
        
        // Verify
        mockManager.Verify(m => m.Dispense(1000m), Times.Once);
        Assert.Equal("", vm.DispenseAmountInput);
    }

    [Fact]
    public void DenominationViewModel_AddCommand_ShouldCallInventoryAdd()
    {
        // Setup
        var mockInventory = new Mock<Inventory>();
        mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<int>());
        
        var denom = 1000;
        var vm = new DenominationViewModel(mockInventory.Object, denom);
        
        // Action
        vm.AddCommand.Execute(Unit.Default);
        
        // Verify
        mockInventory.Verify(i => i.Add(denom, 1), Times.Once);
    }
}
