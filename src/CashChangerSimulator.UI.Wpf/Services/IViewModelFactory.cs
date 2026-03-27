using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>ViewModel のインスタンス生成を担当するファクトリインターフェース。</summary>
public interface IViewModelFactory
{
    DepositViewModel CreateDepositViewModel(Func<IEnumerable<DenominationViewModel>> getDenominations, BindableReactiveProperty<bool> isDispenseBusy);
    DispenseViewModel CreateDispenseViewModel(BindableReactiveProperty<bool> isInDepositMode, Func<IEnumerable<DenominationViewModel>> getDenominations);
    InventoryViewModel CreateInventoryViewModel(ConfigurationProvider configProvider);
    AdvancedSimulationViewModel CreateAdvancedSimulationViewModel(ConfigurationProvider configProvider);
    DenominationViewModel CreateDenominationViewModel(DenominationKey key);
    BulkAmountInputViewModel CreateBulkAmountInputViewModel(
        IEnumerable<BulkAmountInputItemViewModel> items,
        ICommand simulateOverlap,
        ICommand simulateJam,
        ICommand simulateDeviceError,
        ICommand resetError,
        ReadOnlyReactiveProperty<bool> isJammed,
        ReadOnlyReactiveProperty<bool> isOverlapped,
        ReadOnlyReactiveProperty<bool> isDeviceError);
    SettingsViewModel CreateSettingsViewModel();
    BulkAmountInputItemViewModel CreateBulkAmountInputItemViewModel(DenominationKey key, string name);
}
