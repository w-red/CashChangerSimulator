using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests;

/// <summary>ViewModel の動作を検証する単体テスト。</summary>
public class ViewModelTest
{
    /// <summary>払出コマンド実行時にマネージャーが呼ばれ、入力がクリアされることを検証する。</summary>
    [Fact]
    public void MainViewModelDispenseShouldCallManagerAndClearInput()
    {
        // Setup
        var mockInventory = new Mock<Inventory>();
        mockInventory.Setup(i => i.Changed).Returns(Observable.Empty<DenominationKey>());
        mockInventory.Setup(i => i.CalculateTotal()).Returns(0m);

        var mockHistory = new Mock<TransactionHistory>();
        mockHistory.Setup(h => h.Added).Returns(Observable.Empty<TransactionEntry>());

        // プロバイダー系は依存関係を注入した実インスタンスを使用するのが簡単です
        var realConfig = new ConfigurationProvider();
        realConfig.Config.CurrencyCode = "JPY"; // テスト用に明示的に設定
        
        // デフォルトの JPY 設定を使用
        var realMetadata = new Wpf.Services.CurrencyMetadataProvider(realConfig);
        var realMonitors = new MonitorsProvider(mockInventory.Object, realConfig, realMetadata);
        var realAggregator = new OverallStatusAggregatorProvider(realMonitors);

        // Managerのメソッドがvirtualなのでモック可能です
        var mockManager = new Mock<CashChangerManager>(mockInventory.Object, mockHistory.Object);

        var realHardware = new HardwareStatusManager();
        var depositController = new DepositController(mockInventory.Object, mockManager.Object);
        
        var vm = new MainViewModel(
            mockInventory.Object,
            mockHistory.Object,
            mockManager.Object,
            realMonitors,
            realAggregator,
            realConfig,
            realMetadata,
            realHardware,
            depositController)
        {
            // Dispense 1000
            DispenseAmountInput = { Value = "1000" }
        };
        vm.DispenseCommand.Execute(Unit.Default);
        
        // Verify
        mockManager.Verify(m => m.Dispense(1000m), Times.Once);
        vm.DispenseAmountInput.Value.ShouldBe("");
    }

}
