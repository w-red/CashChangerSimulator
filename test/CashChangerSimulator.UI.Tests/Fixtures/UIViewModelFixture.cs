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
using CashChangerSimulator.UI.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PointOfService;
using Moq;
using R3;

namespace CashChangerSimulator.UI.Tests.Fixtures;

/// <summary>UI ViewModel のテスト用共有フィクスチャ。</summary>
public class UIViewModelFixture : IDisposable
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
    public MonitorsProvider Monitors { get; private set; } = null!;
    public IScriptExecutionService ScriptExecutionService { get; private set; } = null!;
    public IDispatcherService DispatcherService { get; private set; } = null!;
    public Mock<INotifyService> NotifyServiceMock { get; private set; } = null!;
    public Mock<IViewService> ViewServiceMock { get; private set; } = null!;
    public Mock<IHistoryExportService> ExportServiceMock { get; private set; } = null!;
    public Mock<IDepositOperationService> DepositServiceMock { get; private set; } = null!;
    public Mock<IDispenseOperationService> DispenseServiceMock { get; private set; } = null!;

    public UIViewModelFixture()
    {
        Initialize();
    }

    /// <summary>共通のセットアップを初期化します。必要に応じて実体のスクリプトサービスを使用可能です。</summary>
    public void Initialize(string currencyCode = "JPY", bool useRealScriptService = false)
    {
        // 既存のインスタンスがあれば破棄してリソースリークを防ぐ
        try { CashChanger?.Close(); } catch { }
        try { CashChanger?.Dispose(); } catch { }
        try { (Monitors as IDisposable)?.Dispose(); } catch { }
        try { ConfigProvider?.Dispose(); } catch { }

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

        NotifyServiceMock = new Mock<INotifyService>();
        ViewServiceMock = new Mock<IViewService>();
        ExportServiceMock = new Mock<IHistoryExportService>();
        DepositServiceMock = new Mock<IDepositOperationService>();
        DispenseServiceMock = new Mock<IDispenseOperationService>();

        if (useRealScriptService)
        {
            ScriptExecutionService = new ScriptExecutionService(DepositController, DispenseController, Inventory, Hardware);
        }
        else
        {
            ScriptExecutionService = new Mock<IScriptExecutionService>().Object;
        }

        DispatcherService = new ImmediateDispatcherService();

        Monitors = new MonitorsProvider(Inventory, ConfigProvider, MetadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(Monitors);

        CashChanger = new InternalSimulatorCashChanger(
            new SimulatorDependencies(
                ConfigProvider,
                Inventory,
                History,
                Manager,
                DepositController,
                null!, // Will be set after creation
                aggregatorProvider,
                Hardware,
                diagnosticController))
        {
            CurrencyCode = currencyCode
        };
        CashChanger.SkipStateVerification = true;

        DispenseController = new DispenseController(Manager, Hardware, CashChanger);

        // Open and enable device for testing
        CashChanger.Open();
        CashChanger.Claim(100);
        CashChanger.DeviceEnabled = true;

        // --- Default Mock Setups (Delegate to Controller) ---
        
        DepositServiceMock.Setup(x => x.BeginDeposit()).Callback(() => DepositController.BeginDeposit());
        DepositServiceMock.Setup(x => x.PauseDeposit(It.IsAny<CashDepositPause>())).Callback<CashDepositPause>(p => DepositController.PauseDeposit(p));
        DepositServiceMock.Setup(x => x.FixDeposit()).Callback(() => DepositController.FixDeposit());
        DepositServiceMock.Setup(x => x.EndDeposit(It.IsAny<CashDepositAction>())).Callback<CashDepositAction>(a => DepositController.EndDeposit(a));
        DepositServiceMock.Setup(x => x.TrackBulkDeposit(It.IsAny<IReadOnlyDictionary<DenominationKey, int>>())).Callback<IReadOnlyDictionary<DenominationKey, int>>(c => DepositController.TrackBulkDeposit(c));
        DepositServiceMock.Setup(x => x.SimulateReject(It.IsAny<decimal>())).Callback<decimal>(a => DepositController.SimulateReject(a));
        
        DispenseServiceMock.Setup(x => x.DispenseCash(It.IsAny<decimal>()))
            .Callback<decimal>(amount => DispenseController.DispenseChangeAsync(amount, true, (c, e) => { }, "JPY"));
        DispenseServiceMock.Setup(x => x.ExecuteBulkDispense(It.IsAny<IReadOnlyDictionary<DenominationKey, int>>()))
            .Callback<IReadOnlyDictionary<DenominationKey, int>>(counts => DispenseController.DispenseCashAsync(counts, true, (c, e) => { }));
    }

    /// <summary>テスト用の在庫データを一括設定します。</summary>
    public void SetInventory(params (DenominationKey key, int count)[] items)
    {
        Inventory.Clear();
        foreach (var (key, count) in items)
        {
            Inventory.SetCount(key, count);
        }
    }

    /// <summary>設定上の初期在庫（Config.InitialCount）を設定します。</summary>
    public void SetConfigInitialCounts(params (string key, int count)[] items)
    {
        var currency = ConfigProvider.Config.System.CurrencyCode;
        if (!ConfigProvider.Config.Inventory.ContainsKey(currency))
        {
            ConfigProvider.Config.Inventory[currency] = new InventorySettings();
        }

        foreach (var (key, count) in items)
        {
            if (!ConfigProvider.Config.Inventory[currency].Denominations.ContainsKey(key))
            {
                ConfigProvider.Config.Inventory[currency].Denominations[key] = new DenominationSettings();
            }
            ConfigProvider.Config.Inventory[currency].Denominations[key].InitialCount = count;
        }
    }

    /// <summary>リセットします。</summary>
    public void Reset()
    {
        Initialize();
    }

    /// <summary>デバイスとコア機能の Facade を生成します。</summary>
    internal IDeviceFacade CreateFacade()
    {
        var aggregatorProvider = new OverallStatusAggregatorProvider(Monitors);
        return new DeviceFacade(
            Inventory,
            Manager,
            DepositController,
            DispenseController,
            Hardware,
            CashChanger,
            History,
            aggregatorProvider,
            Monitors,
            NotifyServiceMock.Object,
            DispatcherService,
            ViewServiceMock.Object);
    }

    /// <summary>検証用の MainViewModel を生成します。</summary>
    internal MainViewModel CreateMainViewModel()
    {
        var services = new ServiceCollection();
        var facade = CreateFacade();
        
        services.AddSingleton(facade);
        services.AddSingleton(ConfigProvider);
        services.AddSingleton(MetadataProvider);
        services.AddSingleton(NotifyServiceMock.Object);
        services.AddSingleton(ExportServiceMock.Object);
        services.AddSingleton(DepositServiceMock.Object);
        services.AddSingleton(DispenseServiceMock.Object);
        services.AddSingleton(ScriptExecutionService);
        services.AddSingleton(DispatcherService);
        
        services.AddTestWpfUiServices();
        
        var provider = services.BuildServiceProvider();
        var factory = new ViewModelFactory(provider);
        services.AddSingleton<IViewModelFactory>(factory);

        // Re-build provider to include the factory
        var finalProvider = services.BuildServiceProvider();
        
        return finalProvider.GetRequiredService<MainViewModel>();
    }

    /// <summary>検証用の InventoryViewModel を生成します。</summary>
    internal InventoryViewModel CreateInventoryViewModel()
    {
        return new InventoryViewModel(CreateFacade(), ConfigProvider, MetadataProvider, ExportServiceMock.Object, NotifyServiceMock.Object);
    }

    /// <summary>検証用の DepositViewModel を生成します。</summary>
    internal DepositViewModel CreateDepositViewModel(Func<IEnumerable<DenominationViewModel>>? denominationsFactory = null, BindableReactiveProperty<bool>? isDispenseBusy = null)
    {
        return new DepositViewModel(
            CreateFacade(),
            denominationsFactory ?? (() => []),
            isDispenseBusy ?? new BindableReactiveProperty<bool>(false),
            NotifyServiceMock.Object,
            DepositServiceMock.Object,
            MetadataProvider);
    }

    /// <summary>検証用の DispenseViewModel を生成します。</summary>
    internal DispenseViewModel CreateDispenseViewModel(BindableReactiveProperty<bool>? isInDepositMode = null, Func<IEnumerable<DenominationViewModel>>? denominationsFactory = null)
    {
        return new DispenseViewModel(
            CreateFacade(),
            ConfigProvider,
            isInDepositMode ?? new BindableReactiveProperty<bool>(false),
            denominationsFactory ?? (() => []),
            NotifyServiceMock.Object,
            DispenseServiceMock.Object,
            MetadataProvider);
    }

    /// <summary>検証用の AdvancedSimulationViewModel を生成します。</summary>
    internal AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(Mock<IScriptExecutionService>? scriptServiceMock = null)
    {
        var scriptService = scriptServiceMock?.Object ?? ScriptExecutionService;
        return new AdvancedSimulationViewModel(CreateFacade(), scriptService, MetadataProvider);
    }

    /// <summary>検証用の SettingsViewModel を生成します。</summary>
    internal SettingsViewModel CreateSettingsViewModel()
    {
        return new SettingsViewModel(ConfigProvider, Monitors, MetadataProvider);
    }

    public void Dispose()
    {
        try { CashChanger?.Close(); } catch { }
        try { CashChanger?.Dispose(); } catch { }
        try { (Monitors as IDisposable)?.Dispose(); } catch { }
        try { ConfigProvider?.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}
