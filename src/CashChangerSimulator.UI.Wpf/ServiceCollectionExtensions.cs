using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>IServiceCollection に対するレイヤー別の登録メソッドを提供するための拡張クラス。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>コアサービス（在庫マネージャー、履歴、計算機等）を登録します。</summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IDispatcherService, WpfDispatcherService>();
        services.AddSingleton<ConfigurationProvider>();
        services.AddSingleton<ICurrencyMetadataProvider, CurrencyMetadataProvider>();
        services.AddSingleton<CurrencyMetadataProvider>();
        services.AddSingleton<MonitorsProvider>();
        services.AddSingleton<OverallStatusAggregatorProvider>();
        services.AddSingleton<INotifyService, Services.WpfNotifyService>();
        services.AddSingleton<IViewService, WpfViewService>();

        services.AddSingleton<Inventory>();
        services.AddSingleton<TransactionHistory>();
        services.AddSingleton<HistoryPersistenceService>();
        services.AddSingleton<ChangeCalculator>();
        services.AddSingleton<CashChangerManager>();
        services.AddSingleton<HardwareStatusManager>();
        services.AddSingleton<DiagnosticController>();
        services.AddSingleton<IHistoryExportService, CsvHistoryExportService>();

        return services;
    }

    /// <summary>シミュレーターおよびデバイス制御（コントローラー、デバイスインスタンス等）を登録します。</summary>
    public static IServiceCollection AddDeviceServices(this IServiceCollection services)
    {
        services.AddSingleton<IDeviceSimulator, HardwareSimulator>();
        services.AddSingleton<DepositController>();
        services.AddSingleton<DispenseController>();

        services.AddSingleton<InternalSimulatorCashChanger>(sp => {
            var deps = new SimulatorDependencies(
                ConfigProvider: sp.GetRequiredService<ConfigurationProvider>(),
                Inventory: sp.GetRequiredService<Inventory>(),
                History: sp.GetRequiredService<TransactionHistory>(),
                Manager: sp.GetRequiredService<CashChangerManager>(),
                DepositController: sp.GetRequiredService<DepositController>(),
                DispenseController: sp.GetRequiredService<DispenseController>(),
                AggregatorProvider: sp.GetRequiredService<OverallStatusAggregatorProvider>(),
                HardwareStatusManager: sp.GetRequiredService<HardwareStatusManager>(),
                DiagnosticController: sp.GetRequiredService<DiagnosticController>()
            );
            return new InternalSimulatorCashChanger(deps);
        });

        services.AddSingleton<SimulatorCashChanger>(sp => sp.GetRequiredService<InternalSimulatorCashChanger>());
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

        return services;
    }

    /// <summary>UI（ViewModels, Facades 等）を登録します。</summary>
    public static IServiceCollection AddWpfUiServices(this IServiceCollection services)
    {
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        services.AddSingleton<IDeviceFacade, DeviceFacade>();
        services.AddSingleton<IDepositOperationService, DepositOperationService>();
        services.AddSingleton<IDispenseOperationService, DispenseOperationService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<AdvancedSimulationViewModel>();

        return services;
    }
}
