using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests.Fixtures;

/// <summary>PosTransactionViewModel のテスト用フィクスチャ。</summary>
public class PosTransactionViewModelFixture : IDisposable
{
    public Inventory Inventory { get; private set; } = null!;
    public TransactionHistory History { get; private set; } = null!;
    public CashChangerManager Manager { get; private set; } = null!;
    public HardwareStatusManager Hardware { get; private set; } = null!;
    public DepositController DepositController { get; private set; } = null!;
    public DispenseController DispenseController { get; private set; } = null!;
    public InternalSimulatorCashChanger CashChanger { get; private set; } = null!;
    public ConfigurationProvider ConfigProvider { get; private set; } = null!;
    public CurrencyMetadataProvider MetadataProvider { get; private set; } = null!;
    public IScriptExecutionService ScriptExecutionService { get; private set; } = null!;
    public Mock<INotifyService> NotifyServiceMock { get; private set; } = null!;

    public PosTransactionViewModelFixture()
    {
        Initialize();
    }

    public void Initialize(string currencyCode = "JPY")
    {
        Inventory = new Inventory();
        History = new TransactionHistory();
        Manager = new CashChangerManager(Inventory, History, new ChangeCalculator());
        Hardware = new HardwareStatusManager();
        
        // Configuration
        ConfigProvider = new ConfigurationProvider();
        ConfigProvider.Config.Inventory.TryAdd(currencyCode, new InventorySettings());
        ConfigProvider.Config.System.CurrencyCode = currencyCode;
        MetadataProvider = new CurrencyMetadataProvider(ConfigProvider);

        DepositController = new DepositController(Inventory, Hardware);
        DispenseController = new DispenseController(Manager, Hardware, new Mock<IDeviceSimulator>().Object);
        var diagnosticController = new DiagnosticController(Inventory, Hardware);

        var monitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);

        ScriptExecutionService = new Mock<IScriptExecutionService>().Object;
        NotifyServiceMock = new Mock<INotifyService>();

        CashChanger = new InternalSimulatorCashChanger(
            new SimulatorDependencies(
                ConfigProvider,
                Inventory,
                History,
                Manager,
                DepositController,
                DispenseController,
                aggregatorProvider,
                Hardware,
                diagnosticController))
        {
            CurrencyCode = currencyCode
        };
        CashChanger.SkipStateVerification = true;
    }

    /// <summary>検証用の ViewModel を生成します。</summary>
    internal PosTransactionViewModel CreateViewModel()
    {
        var monitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        
        var facade = new DeviceFacade(
            Inventory,
            Manager,
            DepositController,
            DispenseController,
            Hardware,
            CashChanger,
            History,
            aggregatorProvider,
            monitorsProvider,
            NotifyServiceMock.Object);

        var isDispenseBusy = new BindableReactiveProperty<bool>(false);
        var isInDepositMode = new BindableReactiveProperty<bool>(false);

        var depVm = new DepositViewModel(
            facade,
            () => [],
            isDispenseBusy,
            NotifyServiceMock.Object,
            MetadataProvider);

        var dispVm = new DispenseViewModel(
            facade,
            ConfigProvider,
            isInDepositMode,
            () => [],
            NotifyServiceMock.Object,
            MetadataProvider);

        return new PosTransactionViewModel(
            facade,
            depVm,
            dispVm,
            MetadataProvider,
            () => [],
            NotifyServiceMock.Object);
    }

    /// <summary>検証用の AdvancedSimulationViewModel を生成します。</summary>
    internal AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(Mock<IScriptExecutionService>? scriptServiceMock = null)
    {
        var scriptService = scriptServiceMock?.Object ?? new Mock<IScriptExecutionService>().Object;
        
        var monitorsProvider = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);

        var facade = new DeviceFacade(
            Inventory,
            Manager,
            DepositController,
            DispenseController,
            Hardware,
            CashChanger,
            History,
            aggregatorProvider,
            monitorsProvider,
            NotifyServiceMock.Object);

        return new AdvancedSimulationViewModel(facade, scriptService, MetadataProvider);
    }

    public void Dispose()
    {
        CashChanger?.Dispose();
        GC.SuppressFinalize(this);
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
