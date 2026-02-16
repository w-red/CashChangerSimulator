using System;
using System.Collections.Generic;
using System.Linq;
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
        
        // Initialize Inventory with Config
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        var inventory = _resolver.Resolve<Inventory>();
        
        // 新しい金種別設定から読み込み
        foreach (var item in configProvider.Config.Inventory.Denominations)
        {
            if (DenominationKey.TryParse(item.Key, out var key) && key != null)
            {
                inventory.SetCount(key, item.Value.InitialCount);
            }
        }

        // 互換性のためのフォールバック（Denominations にない場合のみ）
        foreach (var item in configProvider.Config.Inventory.InitialCounts)
        {
            if (DenominationKey.TryParse(item.Key, out var key) && key != null)
            {
                if (inventory.GetCount(key) == 0) // まだ設定されていない場合
                {
                    inventory.SetCount(key, item.Value);
                }
            }
        }
    }

    public static T Resolve<T>()
    {
        return _resolver.Resolve<T>();
    }
}
