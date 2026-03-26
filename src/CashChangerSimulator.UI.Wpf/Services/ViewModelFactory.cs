using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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

    public InventoryViewModel CreateInventoryViewModel() => _serviceProvider.GetRequiredService<InventoryViewModel>();

    public AdvancedSimulationViewModel CreateAdvancedSimulationViewModel() => _serviceProvider.GetRequiredService<AdvancedSimulationViewModel>();

    public DenominationViewModel CreateDenominationViewModel(DenominationKey key)
    {
        // ActivatorUtilities overrides the `key` parameter during instantiation.
        return ActivatorUtilities.CreateInstance<DenominationViewModel>(_serviceProvider, key);
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
            _serviceProvider, items, simulateOverlap, simulateJam, simulateDeviceError, resetError, isJammed, isOverlapped, isDeviceError);
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
