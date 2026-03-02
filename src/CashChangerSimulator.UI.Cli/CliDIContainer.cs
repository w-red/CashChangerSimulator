using System;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using MicroResolver;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.UI.Cli;

public static class CliDIContainer
{
    private static ObjectResolver _resolver = null!;

    public static void Initialize(string[] args)
    {
        var resolver = ObjectResolver.Create();

        // Logging
        LogProvider.Initialize(new LoggingSettings
        {
            LogLevel = "Information",
            EnableConsole = true,
            EnableFile = false
        });
        // Providers
        resolver.Register<ConfigurationProvider, CliConfigurationProvider>(Lifestyle.Singleton);
        resolver.Register<ICurrencyMetadataProvider, CurrencyMetadataProvider>(Lifestyle.Singleton);
        resolver.Register<MonitorsProvider, MonitorsProvider>(Lifestyle.Singleton);
        resolver.Register<OverallStatusAggregatorProvider, OverallStatusAggregatorProvider>(Lifestyle.Singleton);
        resolver.Register<INotifyService, CliNotifyService>(Lifestyle.Singleton);

        // Core Services
        resolver.Register<Inventory, Inventory>(Lifestyle.Singleton);
        resolver.Register<TransactionHistory, TransactionHistory>(Lifestyle.Singleton);
        resolver.Register<ChangeCalculator, ChangeCalculator>(Lifestyle.Singleton);
        resolver.Register<CashChangerManager, CashChangerManager>(Lifestyle.Singleton);
        resolver.Register<HardwareStatusManager, HardwareStatusManager>(Lifestyle.Singleton);

        // Simulator / Devices
        resolver.Register<SimulatorCashChanger, SimulatorCashChanger>(Lifestyle.Singleton);
        resolver.Register<IDeviceSimulator, CliHardwareSimulator>(Lifestyle.Singleton);
        resolver.Register<DepositController, DepositController>(Lifestyle.Singleton);
        resolver.Register<DispenseController, DispenseController>(Lifestyle.Singleton);
        resolver.Register<IScriptExecutionService, ScriptExecutionService>(Lifestyle.Singleton);
        resolver.Register<CliCommands, CliCommands>(Lifestyle.Transient);

        resolver.Compile();
        _resolver = resolver;

        SimulatorServices.Provider = new CliResolverServiceProvider(_resolver);

        // Override Configuration from args
        var configProvider = _resolver.Resolve<ConfigurationProvider>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--currency" && i + 1 < args.Length)
            {
                var currencyCode = args[++i].ToUpperInvariant();
                configProvider.Config.System.CurrencyCode = currencyCode;
            }
        }

        // Initialize Inventory
        var inventory = _resolver.Resolve<Inventory>();
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
        var history = _resolver.Resolve<TransactionHistory>();
        var historyState = ConfigurationLoader.LoadHistoryState();
        if (historyState?.Entries != null && historyState.Entries.Count > 0)
        {
            history.FromState(historyState);
        }
    }

    public static ObjectResolver Resolver => _resolver;

    public static T Resolve<T>() => _resolver.Resolve<T>();
}

internal sealed class CliResolverServiceProvider(ObjectResolver resolver) : ISimulatorServiceProvider, IServiceProvider
{
    private static readonly System.Reflection.MethodInfo _resolveMethod =
        typeof(ObjectResolver).GetMethod("Resolve", Type.EmptyTypes)!;

    public T Resolve<T>() where T : class => resolver.Resolve<T>();

    public object? GetService(Type serviceType)
    {
        try {
            var generic = _resolveMethod.MakeGenericMethod(serviceType);
            return generic.Invoke(resolver, null);
        } catch {
            return null;
        }
    }
}

public class CliNotifyService : INotifyService
{
    public void ShowWarning(string message, string title)
    {
        var color = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[{title}] {message}");
        Console.ForegroundColor = color;
    }
}
