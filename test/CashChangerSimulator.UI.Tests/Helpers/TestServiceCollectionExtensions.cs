using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>テスト用の IServiceCollection 拡張メソッドを提供します。</summary>
public static class TestServiceCollectionExtensions
{
    /// <summary>UI テストに必要な ViewModel とサービス（モックを含む）を登録します。</summary>
    /// <param name="services">サービスコレクション。</param>
    /// <returns>登録後のサービスコレクション。</returns>
    public static IServiceCollection AddTestWpfUiServices(this IServiceCollection services)
    {
        services.AddLogging();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<DepositViewModel>();
        services.AddTransient<DispenseViewModel>();
        services.AddTransient<AdvancedSimulationViewModel>();

        AddSingletonIfNotRegistered<IViewModelFactory, ViewModelFactory>(services);
        AddSingletonIfNotRegistered<IDepositOperationService, DepositOperationService>(services);
        AddSingletonIfNotRegistered<IDispatcherService, ImmediateDispatcherService>(services);
        AddSingletonIfNotRegistered<IDispenseOperationService, DispenseOperationService>(services);
        AddSingletonIfNotRegistered<IInventoryOperationService, InventoryOperationService>(services);

        AddMockIfNotRegistered<INotifyService>(services);
        AddMockIfNotRegistered<IViewService>(services);
        AddMockIfNotRegistered<IHistoryExportService>(services);
        AddMockIfNotRegistered<IScriptExecutionService>(services);

        return services;
    }

    private static void AddMockIfNotRegistered<T>(IServiceCollection services)
        where T : class
    {
        if (!services.Any(d => d.ServiceType == typeof(T)))
        {
            services.AddSingleton(new Mock<T>().Object);
        }
    }

    private static void AddSingletonIfNotRegistered<TService, TImplementation>(IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (!services.Any(d => d.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService, TImplementation>();
        }
    }
}
