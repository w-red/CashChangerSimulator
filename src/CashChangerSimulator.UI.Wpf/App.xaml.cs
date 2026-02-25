using CashChangerSimulator.Core;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Transactions;
using R3;
using System.IO;
using System.Windows;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>アプリケーションのエントリーポイントおよびライフサイクルを管理するクラス。</summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            // Load configuration and initialize logger first
            var config = ConfigurationLoader.Load();
            LogProvider.Initialize(config.Logging);
            var logger = LogProvider.CreateLogger<App>();

            // Initialize R3 WPF provider with unhandled exception handler
            WpfProviderInitializer.SetDefaultObservableSystem(ex =>
            {
                logger.ZLogError(ex, $"R3 Unhandled Exception");
            });

            DIContainer.Initialize();
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            // Initializing LogProvider might have failed, but we try to log anyway
            var logger = LogProvider.CreateLogger<App>();
            logger.ZLogCritical(ex, $"Startup Crash");
            
            // Still write to file as a last resort since logger might not be fully functional
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_crash.txt"),
                ex.ToString() + "\n\nInner: " + ex.InnerException?.ToString());
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // Only attempt to save if DIContainer was successfully initialized
            var history = DIContainer.Resolve<TransactionHistory>();
            if (history != null)
            {
                var state = history.ToState();
                ConfigurationLoader.SaveHistoryState(state);
            }
        }
        catch (InvalidOperationException)
        {
            // DIContainer not initialized, ignore
        }
        catch (Exception)
        {
            // Other failures safe on exit: ensure application can close even if history saving fails
        }
        base.OnExit(e);
    }
}

