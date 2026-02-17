using System.Windows;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load configuration and initialize logger
        var config = ConfigurationLoader.Load();
        LogProvider.Initialize(config.Logging);

        DIContainer.Initialize();
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var history = DIContainer.Resolve<TransactionHistory>();
            var state = history.ToState();
            ConfigurationLoader.SaveHistoryState(state);
        }
        catch
        {
            // Fail safe on exit
        }
        base.OnExit(e);
    }
}

