using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
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
    public Mock<IScriptExecutionService> ScriptExecutionServiceMock { get; private set; } = null!;
    public Mock<IDepositOperationService> DepositServiceMock { get; private set; } = null!;
    public Mock<IDispenseOperationService> DispenseServiceMock { get; private set; } = null!;
    public Mock<IInventoryOperationService> InventoryServiceMock { get; private set; } = null!;
    public IServiceProvider ServiceProvider => _serviceProvider ??= CreateServiceProvider();
    public IViewModelFactory ViewModelFactory => ServiceProvider.GetRequiredService<IViewModelFactory>();
    private IServiceProvider? _serviceProvider;

    public UIViewModelFixture()
    {
        // Set SynchronizationContext for R3's ObserveOn(SynchronizationContext.Current) in Unit Tests
        SynchronizationContext.SetSynchronizationContext(new ImmediateSynchronizationContext());
        Initialize();
    }

    private class ImmediateSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) => d(state);
        public override void Send(SendOrPostCallback d, object? state) => d(state);
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
        ScriptExecutionServiceMock = new Mock<IScriptExecutionService>();
        ExportServiceMock = new Mock<IHistoryExportService>();
        DepositServiceMock = new Mock<IDepositOperationService>();
        DispenseServiceMock = new Mock<IDispenseOperationService>();
        InventoryServiceMock = new Mock<IInventoryOperationService>();

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

        InventoryServiceMock.Setup(x => x.SimulateJam()).Callback(() => Hardware.SetJammed(true));
        InventoryServiceMock.Setup(x => x.SimulateOverlap()).Callback(() => Hardware.SetOverlapped(true));

        // [FIX] Initialize ServiceProvider after all dependencies and mocks are ready
        try
        {
            _serviceProvider = CreateServiceProvider();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] UIViewModelFixture.Initialize failed to create ServiceProvider: {ex}");
            throw;
        }
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
        return ServiceProvider.GetRequiredService<MainViewModel>();
    }

    /// <summary>テスト用のサービスプロバイダーを構築します。</summary>
    private IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        var facade = CreateFacade();
        
        services.AddSingleton(facade);
        services.AddSingleton(ConfigProvider);
        services.AddSingleton(MetadataProvider);
        services.AddSingleton(NotifyServiceMock.Object);
        services.AddSingleton(ViewServiceMock.Object);
        services.AddSingleton(ExportServiceMock.Object);
        services.AddSingleton(ScriptExecutionServiceMock.Object);
        services.AddSingleton(DispatcherService);
        if (Monitors != null)
        {
            services.AddSingleton(Monitors);
        }
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(MockLogger<>));
        
        services.AddSingleton<IDepositOperationService, DepositOperationService>();
        services.AddSingleton<IDispenseOperationService, DispenseOperationService>();
        services.AddSingleton<IInventoryOperationService, InventoryOperationService>();
        
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<DepositViewModel>();
        services.AddSingleton<DispenseViewModel>();
        services.AddSingleton<AdvancedSimulationViewModel>();
        services.AddSingleton<SettingsViewModel>();
        
        configure?.Invoke(services);
        
        return services.BuildServiceProvider();
    }

    /// <summary>テスト用のロガー実装。</summary>
    private class MockLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>検証用の InventoryViewModel を生成します。</summary>
    internal InventoryViewModel CreateInventoryViewModel()
    {
        return ViewModelFactory.CreateInventoryViewModel(ConfigProvider);
    }

    /// <summary>検証用の DepositViewModel を生成します。</summary>
    internal DepositViewModel CreateDepositViewModel(
        Func<IEnumerable<DenominationViewModel>>? denominationsFactory = null, 
        BindableReactiveProperty<bool>? isDispenseBusy = null,
        IDepositOperationService? depositService = null,
        IInventoryOperationService? inventoryService = null)
    {
        // If specific mocks are provided, we must create a one-off provider to inject them
        var provider = (depositService != null || inventoryService != null)
            ? CreateServiceProvider(services => {
                if (depositService != null) services.AddSingleton(depositService);
                if (inventoryService != null) services.AddSingleton(inventoryService);
              })
            : ServiceProvider;

        var factory = provider.GetRequiredService<IViewModelFactory>();
        return factory.CreateDepositViewModel(
            denominationsFactory ?? (() => []),
            isDispenseBusy ?? new BindableReactiveProperty<bool>(false));
    }

    /// <summary>検証用の DispenseViewModel を生成します。</summary>
    internal DispenseViewModel CreateDispenseViewModel(
        BindableReactiveProperty<bool>? isInDepositMode = null, 
        Func<IEnumerable<DenominationViewModel>>? denominationsFactory = null,
        IDispenseOperationService? dispenseService = null,
        IInventoryOperationService? inventoryService = null)
    {
        var provider = (dispenseService != null || inventoryService != null)
            ? CreateServiceProvider(services => {
                if (dispenseService != null) services.AddSingleton(dispenseService);
                if (inventoryService != null) services.AddSingleton(inventoryService);
              })
            : ServiceProvider;

        var factory = provider.GetRequiredService<IViewModelFactory>();
        return factory.CreateDispenseViewModel(
            isInDepositMode ?? new BindableReactiveProperty<bool>(false),
            denominationsFactory ?? (() => []));
    }

    /// <summary>検証用の AdvancedSimulationViewModel を生成します。</summary>
    internal AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(Mock<IScriptExecutionService>? scriptServiceMock = null)
    {
        var scriptService = scriptServiceMock?.Object ?? ScriptExecutionService;
        return new AdvancedSimulationViewModel(CreateFacade(), ConfigProvider, scriptService, InventoryServiceMock.Object, MetadataProvider);
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
