using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.UI.Wpf.Views;
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

            // Apply language setting
            UpdateLanguage(config.System.CultureCode);

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

    /// <summary>UIの表示言語を更新します。</summary>
    /// <param name="cultureCode">カルチャコード（"en-US", "ja-JP" など）</param>
    public static void UpdateLanguage(string cultureCode)
    {
        if (Current == null) return;

        try
        {
            var resourceName = cultureCode == "ja-JP" ? "Strings.ja-JP.xaml" : "Strings.en-US.xaml";
            var dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/{resourceName}")
            };

            var dictionaries = Current.Resources.MergedDictionaries;

            // Find and replace existing Strings dictionary
            for (int i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.ToString() ?? "";
                if (source.Contains("Strings.en-US.xaml") || source.Contains("Strings.ja-JP.xaml") || source.Contains("Themes/Strings.xaml"))
                {
                    dictionaries[i] = dict;
                    return;
                }
            }

            // If not found, just add it
            dictionaries.Add(dict);
        }
        catch
        {
            // Silently ignore failures in resource loading (primarily for unit tests)
        }
    }
}

