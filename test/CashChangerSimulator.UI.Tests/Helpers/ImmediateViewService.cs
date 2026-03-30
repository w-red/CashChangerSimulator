using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>何もしないことで UI 依存関係を満たす、テスト用の IViewService 実装。</summary>
public class ImmediateViewService : IViewService
{
    /// <inheritdoc/>
    public void ShowSettingsWindow() { }

    /// <inheritdoc/>
    public void ShowDepositWindow(DepositViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations) { }

    /// <inheritdoc/>
    public void ShowDispenseWindow(DispenseViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations) { }

    /// <inheritdoc/>
    public void ShowAdvancedSimulationWindow(AdvancedSimulationViewModel dataContext) { }

    /// <inheritdoc/>
    public string? ShowSaveFileDialog(string defaultExt, string filter, string fileName) => null;

    /// <inheritdoc/>
    public Task ShowDialogAsync(object content, string identifier = "RootDialog") => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ShowRecoveryHelpDialogAsync(InventoryViewModel dataContext) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task ShowDenominationDetailDialogAsync(DenominationViewModel dataContext) => Task.CompletedTask;
}
