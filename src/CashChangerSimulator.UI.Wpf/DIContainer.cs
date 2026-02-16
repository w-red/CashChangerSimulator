using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
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

        // Core Services (Singleton)
        resolver.Register<Inventory, Inventory>(Lifestyle.Singleton);
        resolver.Register<TransactionHistory, TransactionHistory>(Lifestyle.Singleton);
        resolver.Register<CashChangerManager, CashChangerManager>(Lifestyle.Singleton);
        resolver.Register<OverallStatusAggregatorProvider, OverallStatusAggregatorProvider>(Lifestyle.Singleton);

        // ViewModels (Singleton - to ensure consistency between UI and Logic)
        resolver.Register<MainViewModel, MainViewModel>(Lifestyle.Singleton);

        // Compilation
        resolver.Compile();
        _resolver = resolver;
        
        // Initialize Inventory with State or Config
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        var inventory = _resolver.Resolve<Inventory>();
        
        // 1. 保存された状態があれば最優先
        var state = ConfigurationLoader.LoadInventoryState();
        if (state.Counts.Count > 0)
        {
            inventory.LoadFromDictionary(state.Counts);
        }
        else
        {
            // 2. 保存された状態がない場合は設定の初期値を使用
            // 新しい金種別設定から読み込み
            foreach (var item in configProvider.Config.Inventory.Denominations)
            {
                if (DenominationKey.TryParse(item.Key, out var key) && key != null)
                {
                    inventory.SetCount(key, item.Value.InitialCount);
                }
            }
        }
    }

    public static T Resolve<T>()
    {
        return _resolver.Resolve<T>();
    }
}
