using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Moq;

namespace CashChangerSimulator.UI.Tests.Fixtures;

/// <summary>Test class for providing PosTransactionViewModelFixture functionality.</summary>
public class PosTransactionViewModelFixture : IDisposable
{
    public Inventory Inventory { get; private set; } = null!;
    public TransactionHistory History { get; private set; } = null!;
    public CashChangerManager Manager { get; private set; } = null!;
    public HardwareStatusManager Hardware { get; private set; } = null!;
    public DepositController DepositController { get; private set; } = null!;
    public DispenseController DispenseController { get; private set; } = null!;
    public SimulatorCashChanger CashChanger { get; private set; } = null!;
    public ConfigurationProvider ConfigProvider { get; private set; } = null!;
    public CurrencyMetadataProvider MetadataProvider { get; private set; } = null!;

    public void Initialize(string currencyCode = "JPY")
    {
        Inventory = new Inventory();
        History = new TransactionHistory();
        Manager = new CashChangerManager(Inventory, History, new ChangeCalculator());
        Hardware = new HardwareStatusManager();
        DepositController = new DepositController(Inventory);
        
        DispenseController = new DispenseController(Manager, null, new Mock<IDeviceSimulator>().Object);
        ConfigProvider = new ConfigurationProvider();
        ConfigProvider.Config.Inventory.TryAdd(currencyCode, new InventorySettings());
        ConfigProvider.Config.System.CurrencyCode = currencyCode;
        MetadataProvider = new CurrencyMetadataProvider(ConfigProvider);

        var monitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);

        CashChanger = new SimulatorCashChanger(ConfigProvider, Inventory, History, Manager, DepositController, DispenseController, aggregatorProvider, Hardware)
        {
            SkipStateVerification = true,
            CurrencyCode = currencyCode
        };
    }

    public void Dispose()
    {
        // Add disposable cleanup if necessary
    }
}

public static class PosTransactionTestConstants
{
    /// <summary>非同期処理完了待機時間（ミリ秒）。</summary>
    public const int AsyncCompletionWaitMs = 5000;

    /// <summary>テスト用のデフォルト投入金額。</summary>
    public const string TargetAmount = "1000";

    /// <summary>テスト用の釣銭金額。</summary>
    public const int ChangeAmount = 500;

    /// <summary>テスト用の通貨コード。</summary>
    public const string TestCurrencyCode = "JPY";
}