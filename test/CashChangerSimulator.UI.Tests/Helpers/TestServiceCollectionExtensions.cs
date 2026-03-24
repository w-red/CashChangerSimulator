using System.Linq;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        // Infrastructure
        services.AddLogging();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<InventoryViewModel>();
        services.AddTransient<DepositViewModel>();
        services.AddTransient<DispenseViewModel>();
        services.AddTransient<AdvancedSimulationViewModel>();

        // Services (Real implementations for thin wrappers to maintain behavioral consistency in tests)
        services.AddSingletonIfNotRegistered<IViewModelFactory, ViewModelFactory>();
        services.AddSingletonIfNotRegistered<IDepositOperationService, DepositOperationService>();
        services.AddSingletonIfNotRegistered<IDispenseOperationService, DispenseOperationService>();

        // Mocks for other UI services
        services.AddMockIfNotRegistered<INotifyService>();
        services.AddMockIfNotRegistered<IViewService>();
        services.AddMockIfNotRegistered<IHistoryExportService>();
        services.AddMockIfNotRegistered<IScriptExecutionService>();

        return services;
    }

    private static void AddMockIfNotRegistered<T>(this IServiceCollection services) where T : class
    {
        if (!services.Any(d => d.ServiceType == typeof(T)))
        {
            services.AddSingleton<T>(new Mock<T>().Object);
        }
    }

    private static void AddSingletonIfNotRegistered<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        if (!services.Any(d => d.ServiceType == typeof(TService)))
        {
            services.AddSingleton<TService, TImplementation>();
        }
    }
}
