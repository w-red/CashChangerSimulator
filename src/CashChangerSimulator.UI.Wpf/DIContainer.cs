using System;
using System.Collections.Generic;
using System.Linq;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
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
        resolver.Register<MonitorsProvider, MonitorsProvider>(Lifestyle.Singleton);

        // Core Services (Singleton)
        resolver.Register<Inventory, Inventory>(Lifestyle.Singleton);
        resolver.Register<TransactionHistory, TransactionHistory>(Lifestyle.Singleton);
        resolver.Register<CashChangerManager, CashChangerManager>(Lifestyle.Singleton);
        resolver.Register<OverallStatusAggregatorProvider, OverallStatusAggregatorProvider>(Lifestyle.Singleton);

        // ViewModels (Transient)
        resolver.Register<MainViewModel, MainViewModel>(Lifestyle.Transient);

        // Compilation
        resolver.Compile();
        _resolver = resolver;
        
        // Initialize Inventory with Config
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        var inventory = _resolver.Resolve<Inventory>();
        
        foreach (var item in configProvider.Config.Inventory.InitialCounts)
        {
             if (int.TryParse(item.Key, out int denom))
            {
                inventory.SetCount(denom, item.Value);
            }
        }
    }

    public static T Resolve<T>()
    {
        return _resolver.Resolve<T>();
    }
}
