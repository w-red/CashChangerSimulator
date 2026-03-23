using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Device.Services;
using System.IO;
using MaterialDesignThemes.Wpf;
using CashChangerSimulator.UI.Wpf.Controls;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// WPF implementation of IViewService.
/// </summary>
public class WpfViewService(IDispatcherService dispatcher) : IViewService
{
    private Window? GetOwner() => dispatcher.GetActiveWindow() as Window;

    public void ShowSettingsWindow()
    {
        var window = new SettingsWindow { Owner = GetOwner() };
        window.ShowDialog();
    }

    public void ShowDepositWindow(DepositViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        var window = new DepositWindow(dataContext, getDenominations)
        {
            Owner = GetOwner()
        };
        window.Show();
    }

    public void ShowDispenseWindow(DispenseViewModel dataContext, Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        var window = new DispenseWindow(dataContext, getDenominations)
        {
            Owner = GetOwner()
        };
        window.Show();
    }

    public void ShowAdvancedSimulationWindow(AdvancedSimulationViewModel dataContext)
    {
        var window = new AdvancedSimulationWindow(dataContext);
        window.Show();
    }

    public async Task ShowDialogAsync(object content, string identifier = "RootDialog")
    {
        await DialogHost.Show(content, identifier);
    }

    public Task ShowRecoveryHelpDialogAsync(InventoryViewModel dataContext)
    {
        var view = new RecoveryHelpContent { DataContext = dataContext };
        return ShowDialogAsync(view, "RootDialog");
    }

    public Task ShowDenominationDetailDialogAsync(DenominationViewModel dataContext)
    {
        var view = new DenominationDetailView { DataContext = dataContext };
        return ShowDialogAsync(view, "RootDialog");
    }

    public string? ShowSaveFileDialog(string defaultExt, string filter, string fileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            DefaultExt = defaultExt,
            Filter = filter,
            FileName = fileName
        };

        return dialog.ShowDialog(GetOwner()) == true ? dialog.FileName : null;
    }
}
