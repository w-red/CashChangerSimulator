using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using R3;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary><see cref="IViewModelFactory"/> の実装。DIコンテナを使用してインスタンスを生成します。</summary>
public class ViewModelFactory : IViewModelFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ViewModelFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public DepositViewModel CreateDepositViewModel(Func<IEnumerable<DenominationViewModel>> getDenominations, BindableReactiveProperty<bool> isDispenseBusy)
    {
        return ActivatorUtilities.CreateInstance<DepositViewModel>(_serviceProvider, getDenominations, isDispenseBusy);
    }

    public DispenseViewModel CreateDispenseViewModel(BindableReactiveProperty<bool> isInDepositMode, Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        return ActivatorUtilities.CreateInstance<DispenseViewModel>(_serviceProvider, isInDepositMode, getDenominations);
    }

    public InventoryViewModel CreateInventoryViewModel(ConfigurationProvider configProvider)
    {
        return ActivatorUtilities.CreateInstance<InventoryViewModel>(_serviceProvider, configProvider);
    }

    public AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(ConfigurationProvider configProvider)
    {
        return ActivatorUtilities.CreateInstance<AdvancedSimulationViewModel>(_serviceProvider, configProvider);
    }

    public DenominationViewModel CreateDenominationViewModel(DenominationKey key)
    {
        var facade = _serviceProvider.GetRequiredService<IDeviceFacade>();
        var monitor = facade.Monitors.Monitors.FirstOrDefault(m => m.Key == key)
            ?? throw new InvalidOperationException($"Monitor not found for key: {key}");
        
        return ActivatorUtilities.CreateInstance<DenominationViewModel>(_serviceProvider, key, monitor);
    }

    public BulkAmountInputViewModel CreateBulkAmountInputViewModel(
        IEnumerable<BulkAmountInputItemViewModel> items,
        ICommand simulateOverlap,
        ICommand simulateJam,
        ICommand simulateDeviceError,
        ICommand resetError,
        ReadOnlyReactiveProperty<bool> isJammed,
        ReadOnlyReactiveProperty<bool> isOverlapped,
        ReadOnlyReactiveProperty<bool> isDeviceError)
    {
        return ActivatorUtilities.CreateInstance<BulkAmountInputViewModel>(
            _serviceProvider, 
            items, 
            simulateOverlap, 
            simulateJam, 
            simulateDeviceError, 
            resetError, 
            isJammed, 
            isOverlapped, 
            isDeviceError);
    }

    public SettingsViewModel CreateSettingsViewModel()
    {
        return ActivatorUtilities.CreateInstance<SettingsViewModel>(_serviceProvider);
    }

    public BulkAmountInputItemViewModel CreateBulkAmountInputItemViewModel(DenominationKey key, string name)
    {
        return ActivatorUtilities.CreateInstance<BulkAmountInputItemViewModel>(_serviceProvider, key, name);
    }
}
