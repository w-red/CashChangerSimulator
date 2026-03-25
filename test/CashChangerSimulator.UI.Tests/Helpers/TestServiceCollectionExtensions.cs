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
    extension(IServiceCollection services)
    {
        /// <summary>UI テストに必要な ViewModel とサービス（モックを含む）を登録します。</summary>
        /// <returns>登録後のサービスコレクション。</returns>
        public IServiceCollection AddTestWpfUiServices()
        {
            // 'services' 引数を直接使用
            services.AddLogging();

            // ViewModels
            services.AddTransient<MainViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<DepositViewModel>();
            services.AddTransient<DispenseViewModel>();
            services.AddTransient<AdvancedSimulationViewModel>();

            // extension 内のヘルパーメソッドを呼び出し
            services.AddSingletonIfNotRegistered<IViewModelFactory, ViewModelFactory>();
            services.AddSingletonIfNotRegistered<IDepositOperationService, DepositOperationService>();
            services.AddSingletonIfNotRegistered<IDispatcherService, WpfDispatcherService>();
            services.AddSingletonIfNotRegistered<IDispenseOperationService, DispenseOperationService>();
            services.AddSingletonIfNotRegistered<IInventoryOperationService, InventoryOperationService>();

            services.AddMockIfNotRegistered<INotifyService>();
            services.AddMockIfNotRegistered<IViewService>();
            services.AddMockIfNotRegistered<IHistoryExportService>();
            services.AddMockIfNotRegistered<IScriptExecutionService>();

            return services;
        }

        private void AddMockIfNotRegistered<T>()
            where T : class
        {
            if (!services.Any(d => d.ServiceType == typeof(T)))
            {
                services.AddSingleton(new Mock<T>().Object);
            }
        }

        private void AddSingletonIfNotRegistered<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            if (!services.Any(d => d.ServiceType == typeof(TService)))
            {
                services.AddSingleton<TService, TImplementation>();
            }
        }
    }
}
