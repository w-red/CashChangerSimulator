using System.IO;
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
        try 
        {
            base.OnStartup(e);

            // Load configuration and initialize logger
            var config = ConfigurationLoader.Load();
            LogProvider.Initialize(config.Logging);

            DIContainer.Initialize();
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_crash.txt"), 
                ex.ToString() + "\n\nInner: " + ex.InnerException?.ToString());
            Shutdown();
        }
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

