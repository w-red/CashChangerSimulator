using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>依存関係の注入（DI）を管理する静的コンテナクラス。</summary>
public static class DIContainer
{
    private static IServiceProvider _serviceProvider = null!;

    /// <summary>内部テスト用にサービスプロバイダーを直接設定します。</summary>
    internal static void SetProvider(IServiceProvider provider)
    {
        _serviceProvider = provider;
        SimulatorServices.Provider = new MSIServiceProvider(_serviceProvider);
    }

    /// <summary>コンテナを初期化し、各サービスの登録と解決を行います。</summary>
    public static void Initialize()
    {
        try
        {
            var services = new ServiceCollection();

            // Use modular registration extensions
            services.AddCoreServices();
            services.AddDeviceServices();
            services.AddWpfUiServices();

            // Register Logging from the shared LogProvider
            services.AddSingleton(LogProvider.Factory);
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

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
            var persistence = _serviceProvider.GetRequiredService<HistoryPersistenceService>();
            
            var historyState = persistence.Load();
            if (historyState.Entries.Count > 0)
            {
                history.FromState(historyState);
            }

            // Start Auto-Save
            persistence.StartAutoSave();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[DI_INIT_FATAL] {ex}");
            throw; // Re-throw to let App.OnStartup handle it with its own logging
        }
    }

    /// <summary>指定された型のインスタンスをコンテナから解決します。</summary>
    public static T Resolve<T>() where T : notnull
    {
        return _serviceProvider == null
            ? throw new InvalidOperationException("DIContainer is not initialized yet. Call DIContainer.Initialize() first.")
            : _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>コンテナを破棄し、管理しているサービスのDisposeを呼び出します。</summary>
    public static void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
}

/// <summary>Microsoft.Extensions.DependencyInjection ベースの ISimulatorServiceProvider 実装。</summary>
internal sealed class MSIServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider
{
    public T Resolve<T>() where T : class => provider.GetRequiredService<T>();
}
