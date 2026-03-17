using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// Provides abstraction for UI-related operations like showing windows and dialogs.
/// </summary>
public interface IViewService
{
    /// <summary>Shows the settings window.</summary>
    void ShowSettingsWindow();

    /// <summary>Shows the deposit window.</summary>
    void ShowDepositWindow(DepositViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations);

    /// <summary>Shows the dispense window.</summary>
    void ShowDispenseWindow(DispenseViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations);

    /// <summary>Shows the advanced simulation window.</summary>
    void ShowAdvancedSimulationWindow(AdvancedSimulationViewModel dataContext);

    /// <summary>Shows a dialog with the specified content.</summary>
    Task ShowDialogAsync(object content, string identifier = "RootDialog");
}
