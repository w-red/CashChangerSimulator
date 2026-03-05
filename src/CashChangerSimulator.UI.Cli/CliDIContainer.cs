using Spectre.Console;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.UI.Cli.Localization;
using CashChangerSimulator.UI.Cli.Services;
using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.UI.Cli;

public static class CliDIContainer
{
    private static IServiceProvider _serviceProvider = null!;

    public static void Initialize(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services, args);
        _serviceProvider = services.BuildServiceProvider();
        
        SimulatorServices.Provider = new CliResolverServiceProvider(_serviceProvider);
    }

    public static void ConfigureServices(IServiceCollection services, string[] args)
    {
        // Logging
        LogProvider.Initialize(new LoggingSettings
        {
            LogLevel = "Information",
            EnableConsole = true,
            EnableFile = false
        });

        // Providers
        services.AddSingleton<ConfigurationProvider, CliConfigurationProvider>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();
        services.AddSingleton<MonitorsProvider>();
        services.AddSingleton<OverallStatusAggregatorProvider>();
        services.AddSingleton<INotifyService, CliNotifyService>();
        services.AddSingleton<CliSessionOptions>();
        services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        
        // Localization
        services.AddSingleton<IStringLocalizerFactory, TomlStringLocalizerFactory>();
        services.AddTransient<IStringLocalizer>(sp => sp.GetRequiredService<IStringLocalizerFactory>().Create(typeof(CliCommands)));

        // Core Services
        services.AddSingleton<Inventory>();
        services.AddSingleton<TransactionHistory>();
        services.AddSingleton<ChangeCalculator>();
        services.AddSingleton<CashChangerManager>();
        services.AddSingleton<HardwareStatusManager>();

        // Simulator / Devices
        services.AddSingleton<SimulatorCashChanger, InternalSimulatorCashChanger>();
        services.AddSingleton(sp => (InternalSimulatorCashChanger)sp.GetRequiredService<SimulatorCashChanger>());
        services.AddSingleton<IDeviceSimulator, CliHardwareSimulator>();
        services.AddSingleton<DepositController>();
        services.AddSingleton<DispenseController>();
        services.AddSingleton<DeviceEventHistoryObserver>();
        // CLI Services
        services.AddSingleton<CliDeviceService>();
        services.AddSingleton<CliCashService>();
        services.AddSingleton<CliConfigService>();
        services.AddSingleton<CliViewService>();
        services.AddSingleton<CliScriptService>();

        services.AddTransient<CliCommands>();
    }

    public static void PostInitialize(IServiceProvider provider, string[] args)
    {
        _serviceProvider = provider;
        
        // Override Configuration from args
        var configProvider = provider.GetRequiredService<ConfigurationProvider>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--currency" && i + 1 < args.Length)
            {
                var currencyCode = args[++i].ToUpperInvariant();
                configProvider.Config.System.CurrencyCode = currencyCode;
            }
        }

        // Initialize Inventory
        var inventory = provider.GetRequiredService<Inventory>();
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

        // Initialize History
        var history = provider.GetRequiredService<TransactionHistory>();
        var historyState = ConfigurationLoader.LoadHistoryState();
        if (historyState?.Entries != null && historyState.Entries.Count > 0)
        {
            history.FromState(historyState);
        }

        // Initialize Observers
        provider.GetRequiredService<DeviceEventHistoryObserver>();
    }

    public static IServiceProvider ServiceProvider => _serviceProvider;

    public static T Resolve<T>() where T : notnull => _serviceProvider.GetRequiredService<T>();
}

internal sealed class CliResolverServiceProvider(IServiceProvider provider) : ISimulatorServiceProvider, IServiceProvider
{
    public T Resolve<T>() where T : class => provider.GetRequiredService<T>();

    public object? GetService(Type serviceType) => provider.GetService(serviceType);
}

public class CliNotifyService : INotifyService
{
    public void ShowWarning(string message, string title)
    {
        AnsiConsole.MarkupLine($"[yellow][[{title}]] {message}[/]");
    }
}
