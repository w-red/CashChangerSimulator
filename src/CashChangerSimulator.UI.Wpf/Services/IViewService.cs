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

    /// <summary>Shows the recovery help dialog.</summary>
    Task ShowRecoveryHelpDialogAsync(InventoryViewModel dataContext);

    /// <summary>Shows the denomination detail dialog.</summary>
    Task ShowDenominationDetailDialogAsync(DenominationViewModel dataContext);

    /// <summary>Shows a save file dialog.</summary>
    /// <param name="defaultExt">Default file extension.</param>
    /// <param name="filter">File filter string (e.g., "CSV files (*.csv)|*.csv").</param>
    /// <param name="fileName">Initial file name.</param>
    /// <returns>The full path of the selected file, or null if canceled.</returns>
    string? ShowSaveFileDialog(string defaultExt, string filter, string fileName);
}
