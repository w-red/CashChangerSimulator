using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using MicroResolver;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>依存関係の注入（DI）を管理する静的コンテナクラス。</summary>
public static class DIContainer
{
    private static ObjectResolver _resolver = null!;

    /// <summary>コンテナを初期化し、各サービスの登録と解決を行います。</summary>
    public static void Initialize()
    {
        var resolver = ObjectResolver.Create();

        // 1. Providers (Singleton)
        resolver.Register<ConfigurationProvider, ConfigurationProvider>(Lifestyle.Singleton);
        resolver.Register<CurrencyMetadataProvider, CurrencyMetadataProvider>(Lifestyle.Singleton);
        resolver.Register<MonitorsProvider, MonitorsProvider>(Lifestyle.Singleton);
        resolver.Register<OverallStatusAggregatorProvider, OverallStatusAggregatorProvider>(Lifestyle.Singleton);
        resolver.Register<INotifyService, Services.WpfNotifyService>(Lifestyle.Singleton);

        // 2. Core Services (Singleton)
        resolver.Register<Inventory, Inventory>(Lifestyle.Singleton);
        resolver.Register<TransactionHistory, TransactionHistory>(Lifestyle.Singleton);
        resolver.Register<ChangeCalculator, ChangeCalculator>(Lifestyle.Singleton);
        resolver.Register<CashChangerManager, CashChangerManager>(Lifestyle.Singleton);
        resolver.Register<HardwareStatusManager, HardwareStatusManager>(Lifestyle.Singleton);

        // 3. Simulator / Devices (Singleton)
        resolver.Register<SimulatorCashChanger, SimulatorCashChanger>(Lifestyle.Singleton);
        resolver.Register<IDeviceSimulator, HardwareSimulator>(Lifestyle.Singleton);
        resolver.Register<DepositController, DepositController>(Lifestyle.Singleton);
        resolver.Register<DispenseController, DispenseController>(Lifestyle.Singleton);
        resolver.Register<IScriptExecutionService, ScriptExecutionService>(Lifestyle.Singleton);

        // 4. ViewModels (Singleton - to ensure consistency between UI and Logic)
        resolver.Register<MainViewModel, MainViewModel>(Lifestyle.Singleton);

        // Compilation
        resolver.Compile();
        _resolver = resolver;

        // Register as SimulatorServices provider for cross-project service sharing
        SimulatorServices.Provider = new ResolverServiceProvider(_resolver);

        // Initialize Inventory with State or Config
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        var inventory = _resolver.Resolve<Inventory>();

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

    /// <summary>指定された型のインスタンスをコンテナから解決します。</summary>
    public static T Resolve<T>()
    {
        return _resolver == null
            ? throw new InvalidOperationException("DIContainer is not initialized yet. Call DIContainer.Initialize() first.")
            : _resolver.Resolve<T>();
    }
}

/// <summary>MicroResolver ベースの ISimulatorServiceProvider 実装。</summary>
/// <param name="resolver">MicroResolver のリゾルバーインスタンス。</param>
internal sealed class ResolverServiceProvider(ObjectResolver resolver) : ISimulatorServiceProvider
{
    public T Resolve<T>() where T : class => resolver.Resolve<T>();
}
