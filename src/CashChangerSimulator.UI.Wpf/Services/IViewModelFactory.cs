using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>ViewModel のインスタンス生成を担当するファクトリインターフェース。</summary>
public interface IViewModelFactory
{
    DepositViewModel CreateDepositViewModel(Func<IEnumerable<DenominationViewModel>> getDenominations, BindableReactiveProperty<bool> isDispenseBusy);
    DispenseViewModel CreateDispenseViewModel(BindableReactiveProperty<bool> isInDepositMode, Func<IEnumerable<DenominationViewModel>> getDenominations);
    InventoryViewModel CreateInventoryViewModel();
    AdvancedSimulationViewModel CreateAdvancedSimulationViewModel();
    DenominationViewModel CreateDenominationViewModel(DenominationKey key);
}
