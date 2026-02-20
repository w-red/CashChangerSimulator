using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using MicroResolver;

namespace CashChangerSimulator.UI.Wpf;

public static class DIContainer
{
    private static ObjectResolver _resolver = null!;

    public static void Initialize()
    {
        var resolver = ObjectResolver.Create();

        // Providers (Singleton)
        resolver.Register<ConfigurationProvider, ConfigurationProvider>(Lifestyle.Singleton);
        resolver.Register<CurrencyMetadataProvider, CurrencyMetadataProvider>(Lifestyle.Singleton);
        resolver.Register<MonitorsProvider, MonitorsProvider>(Lifestyle.Singleton);

        // Configuration-related
        resolver.Register<SimulationSettings, SimulationSettings>(Lifestyle.Singleton);

        // Core Services (Singleton)
        resolver.Register<Inventory, Inventory>(Lifestyle.Singleton);
        resolver.Register<TransactionHistory, TransactionHistory>(Lifestyle.Singleton);
        resolver.Register<CashChangerManager, CashChangerManager>(Lifestyle.Singleton);
        resolver.Register<HardwareStatusManager, HardwareStatusManager>(Lifestyle.Singleton);
        resolver.Register<OverallStatusAggregatorProvider, OverallStatusAggregatorProvider>(Lifestyle.Singleton);
        resolver.Register<DepositController, DepositController>(Lifestyle.Singleton);
        resolver.Register<DispenseController, DispenseController>(Lifestyle.Singleton);

        // ViewModels (Singleton - to ensure consistency between UI and Logic)
        resolver.Register<MainViewModel, MainViewModel>(Lifestyle.Singleton);

        // Compilation
        resolver.Compile();
        _resolver = resolver;

        // Populate ServiceLocator for cross-project singleton sharing
        Core.ServiceLocator.Inventory = _resolver.Resolve<Inventory>();
        Core.ServiceLocator.History = _resolver.Resolve<TransactionHistory>();
        Core.ServiceLocator.Manager = _resolver.Resolve<CashChangerManager>();
        Core.ServiceLocator.HardwareStatusManager = _resolver.Resolve<HardwareStatusManager>();

        // Initialize Inventory with State or Config
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        var inventory = _resolver.Resolve<Inventory>();

        // Initialize Simulation Settings from Config
        var simSettings = _resolver.Resolve<SimulationSettings>();
        var simConfig = configProvider.Config.Simulation;
        simSettings.DelayEnabled = simConfig.DelayEnabled;
        simSettings.MinDelayMs = simConfig.MinDelayMs;
        simSettings.MaxDelayMs = simConfig.MaxDelayMs;
        simSettings.RandomErrorsEnabled = simConfig.RandomErrorsEnabled;
        simSettings.ErrorRate = simConfig.ErrorRate;
        simSettings.ValidationFailureRate = simConfig.ValidationFailureRate;

        // 1. 保存された状態があれば最優先
        var state = ConfigurationLoader.LoadInventoryState();
        if (state?.Counts != null && state.Counts.Count > 0)
        {
            inventory.LoadFromDictionary(state.Counts);
        }
        else
        {
            // 2. 保存された状態がない場合は設定の初期値を使用
            // 新しい金種別設定から読み込み（すべての通貨を対象に）
            foreach (var currencyEntry in configProvider.Config.Inventory)
            {
                var currencyCode = currencyEntry.Key;
                foreach (var item in currencyEntry.Value.Denominations)
                {
                    if (DenominationKey.TryParse(item.Key, currencyCode, out var key) && key != null)
                    {
                        inventory.SetCount(key, item.Value.InitialCount);
                    }
                }
            }
        }

        // Initialize Transaction History
        var history = _resolver.Resolve<TransactionHistory>();
        var historyState = ConfigurationLoader.LoadHistoryState();
        if (historyState?.Entries != null && historyState.Entries.Count > 0)
        {
            history.FromState(historyState);
        }
    }

    public static T Resolve<T>()
    {
        return _resolver.Resolve<T>();
    }
}
