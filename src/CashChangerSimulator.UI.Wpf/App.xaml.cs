using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Device;
using R3;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>アプリケーションのエントリーポイントおよびライフサイクルを管理するクラス。</summary>
internal partial class App : Application
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

            // Apply theme setting safely after window is created
            Dispatcher.BeginInvoke(new Action(() => 
            {
                UpdateTheme(config.System.BaseTheme);
                ObserveThemeTrigger();
            }));

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

    private void ObserveThemeTrigger()
    {
        var triggerFile = Environment.GetEnvironmentVariable("TEST_AUTO_CHANGE_THEME_FILE");
        if (string.IsNullOrEmpty(triggerFile)) return;

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(1000);
                if (System.IO.File.Exists(triggerFile))
                {
                    try
                    {
                        var theme = System.IO.File.ReadAllText(triggerFile).Trim();
                        System.IO.File.Delete(triggerFile);
                        
                        Dispatcher.Invoke(() => UpdateTheme(theme));
                    }
                    catch { }
                }
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // [FIX] Explicitly close all windows (except the main one which might already be closing)
            // to ensure ViewModels associated with those windows can start their cleanup.
            var windows = Windows.Cast<Window>().ToList();
            foreach (var window in windows)
            {
                if (window != MainWindow)
                {
                    try { window.Close(); } catch { }
                }
            }

            // [FIX] Explicitly close the CashChanger device if it's still open.
            // This helps the POS SDK state transitions happen before the container starts tearing down dependencies.
            try
            {
                var cashChanger = DIContainer.Resolve<SimulatorCashChanger>();
                if (cashChanger.State != Microsoft.PointOfService.ControlState.Closed)
                {
                    cashChanger.Close();
                }
            }
            catch { /* Ignore if device was not initialized or failure during close */ }
        }
        catch (Exception)
        {
            // Ignore exit-time cleanup errors to ensure the process actually terminates
        }
        finally
        {
            DIContainer.Dispose();
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

    /// <summary>UIのベーステーマを更新します。</summary>
    /// <param name="themeName">"Dark" または "Light"</param>
    public static void UpdateTheme(string themeName)
    {
        if (Current == null) return;

        try
        {
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = paletteHelper.GetTheme();
            
            var isLight = themeName.Equals("Light", StringComparison.OrdinalIgnoreCase);
            var baseTheme = isLight 
                ? MaterialDesignThemes.Wpf.BaseTheme.Light 
                : MaterialDesignThemes.Wpf.BaseTheme.Dark;

            MaterialDesignThemes.Wpf.ThemeExtensions.SetBaseTheme(theme, baseTheme);

            // Dynamically adjust Primary/Secondary colors for each mode
            if (isLight)
            {
                // Vibrant Deep Purple and Deep Emerald for Light Mode
                theme.SetPrimaryColor(System.Windows.Media.Color.FromRgb(0x62, 0x00, 0xEE));
                theme.SetSecondaryColor(System.Windows.Media.Color.FromRgb(0x01, 0x87, 0x86));
                theme.SetTertiaryColor(System.Windows.Media.Color.FromRgb(0x03, 0xDA, 0xC6));
            }
            else
            {
                // Soft light purple for dark mode, mint green for secondary
                theme.SetPrimaryColor(System.Windows.Media.Color.FromRgb(0xBB, 0x86, 0xFC));
                theme.SetSecondaryColor(System.Windows.Media.Color.FromRgb(0x03, 0xDA, 0xC6));
                theme.SetTertiaryColor(System.Windows.Media.Color.FromRgb(0xFF, 0xAB, 0x91)); // Deep Orange for accents in dark
            }

            paletteHelper.SetTheme(theme);
        }
        catch (Exception ex)
        {
            // ZLogger の ZLogError は構造化ログの最適化のため ref を要求する場合がある。
            // ここでは最も互換性の高い標準の LogError を使用する（Microsoft.Extensions.Logging の拡張メソッド）。
            LogProvider.CreateLogger<App>().LogError(ex, "Failed to update theme to {Theme}", themeName);
        }
    }

    // Removed ObserveThemeTrigger to avoid background thread interference during startup
}

