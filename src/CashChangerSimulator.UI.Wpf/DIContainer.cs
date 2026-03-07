using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>依存関係の注入（DI）を管理する静的コンテナクラス。</summary>
public static class DIContainer
{
    private static IServiceProvider _serviceProvider = null!;

    /// <summary>コンテナを初期化し、各サービスの登録と解決を行います。</summary>
    public static void Initialize()
    {
        var services = new ServiceCollection();

        // 1. Providers (Singleton)
        services.AddSingleton<ConfigurationProvider>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();
        services.AddSingleton<CurrencyMetadataProvider>();
        services.AddSingleton<MonitorsProvider>();
        services.AddSingleton<OverallStatusAggregatorProvider>();
        services.AddSingleton<INotifyService, Services.WpfNotifyService>();
        services.AddSingleton<SimulatorDependencies>();

        // 2. Core Services (Singleton)
        services.AddSingleton<Inventory>();
        services.AddSingleton<TransactionHistory>();
        services.AddSingleton<ChangeCalculator>();
        services.AddSingleton<CashChangerManager>();
        services.AddSingleton<HardwareStatusManager>();
        services.AddSingleton<DiagnosticController>();

        // 3. Simulator / Devices (Singleton)
        // Manual instantiation via factory to handle complex dependencies if needed, 
        // though MSI handles standard constructor injection much better than the previous DI container.
        services.AddSingleton<InternalSimulatorCashChanger>(sp => {
            var deps = sp.GetRequiredService<SimulatorDependencies>();
            return new InternalSimulatorCashChanger(deps);
        });

        services.AddSingleton<SimulatorCashChanger>(sp => sp.GetRequiredService<InternalSimulatorCashChanger>());
        services.AddSingleton<IDeviceSimulator, HardwareSimulator>();
        services.AddSingleton<DepositController>();
        services.AddSingleton<DispenseController>();
        services.AddSingleton<DeviceEventHistoryObserver>();
        services.AddSingleton<IScriptExecutionService, ScriptExecutionService>();

        services.AddSingleton<IUposConfigurationManager>(sp => {
            var so = sp.GetRequiredService<SimulatorCashChanger>();
            var config = sp.GetRequiredService<ConfigurationProvider>();
            var inventory = sp.GetRequiredService<Inventory>();
            return new UposConfigurationManager(config, inventory, (IDeviceStateProvider)so);
        });

        services.AddSingleton<IUposEventNotifier>(sp => {
            var so = sp.GetRequiredService<SimulatorCashChanger>();
            return new UposEventNotifier((IUposEventSink)so);
        });

        services.AddSingleton<IUposMediator>(sp => {
            var so = sp.GetRequiredService<SimulatorCashChanger>();
            return new UposMediator(so);
        });

        // 4. ViewModels (Singleton - to ensure consistency between UI and Logic)
        services.AddSingleton<MainViewModel>();

        // Build the ServiceProvider
        _serviceProvider = services.BuildServiceProvider();

        // Register as SimulatorServices provider for cross-project service sharing
        SimulatorServices.Provider = new MSIServiceProvider(_serviceProvider);

        // Initialization logic
        var configProvider = _serviceProvider.GetRequiredService<ConfigurationProvider>();
        var inventory = _serviceProvider.GetRequiredService<Inventory>();

        // Ensure the event history observer is instantiated and listening
        _serviceProvider.GetRequiredService<DeviceEventHistoryObserver>();

        if (Environment.GetEnvironmentVariable("SKIP_STATE_VERIFICATION") == "true")
        {
            _serviceProvider.GetRequiredService<SimulatorCashChanger>().SkipStateVerification = true;
        }

        // Load Inventory State
        var state = ConfigurationLoader.LoadInventoryState();
        if (state?.Counts != null && state.Counts.Count > 0)
        {
            inventory.LoadFromDictionary(state.Counts);
        }
        else
        {
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
        var history = _serviceProvider.GetRequiredService<TransactionHistory>();
        var historyState = ConfigurationLoader.LoadHistoryState();
        if (historyState?.Entries != null && historyState.Entries.Count > 0)
        {
            history.FromState(historyState);
        }
    }

    /// <summary>指定された型のインスタンスをコンテナから解決します。</summary>
    public static T Resolve<T>() where T : notnull
    {
        return _serviceProvider == null
            ? throw new InvalidOperationException("DIContainer is not initialized yet. Call DIContainer.Initialize() first.")
            : _serviceProvider.GetRequiredService<T>();
    }
}

/// <summary>Microsoft.Extensions.DependencyInjection ベースの ISimulatorServiceProvider 実装。</summary>
internal sealed class MSIServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider
{
    public T Resolve<T>() where T : class => provider.GetRequiredService<T>();
}
