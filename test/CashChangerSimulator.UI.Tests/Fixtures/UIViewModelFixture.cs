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

    public UIViewModelFixture()
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

        NotifyServiceMock = new Mock<INotifyService>();
        ViewServiceMock = new Mock<IViewService>();
        ScriptExecutionService = new Mock<IScriptExecutionService>().Object;
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
        services.AddSingleton(ScriptExecutionService);
        services.AddSingleton(DispatcherService);
        
        // Register ViewModels for the factory to resolve
        services.AddTransient<MainViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<DepositViewModel>();
        services.AddTransient<DispenseViewModel>();
        services.AddTransient<AdvancedSimulationViewModel>();
        
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
        return new InventoryViewModel(CreateFacade(), ConfigProvider, MetadataProvider, NotifyServiceMock.Object);
    }

    /// <summary>検証用の DepositViewModel を生成します。</summary>
    internal DepositViewModel CreateDepositViewModel(Func<IEnumerable<DenominationViewModel>>? denominationsFactory = null, BindableReactiveProperty<bool>? isDispenseBusy = null)
    {
        return new DepositViewModel(
            CreateFacade(),
            denominationsFactory ?? (() => []),
            isDispenseBusy ?? new BindableReactiveProperty<bool>(false),
            NotifyServiceMock.Object,
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
            MetadataProvider);
    }

    /// <summary>検証用の AdvancedSimulationViewModel を生成します。</summary>
    internal AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(Mock<IScriptExecutionService>? scriptServiceMock = null)
    {
        var scriptService = scriptServiceMock?.Object ?? ScriptExecutionService;
        return new AdvancedSimulationViewModel(CreateFacade(), scriptService, MetadataProvider);
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
