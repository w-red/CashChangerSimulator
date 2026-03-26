using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Device;
using R3;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using ZLogger;
using System.Windows.Media;

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
                HandleFatalException(ex);
            });

            // Global Unhandled Exception Handlers
            AppDomain.CurrentDomain.UnhandledException += (s, ev) => HandleFatalException(ev.ExceptionObject as Exception);
            DispatcherUnhandledException += (s, ev) => { HandleFatalException(ev.Exception); ev.Handled = false; };

            DIContainer.Initialize();

            // Apply language setting
            UpdateLanguage(config.System.CultureCode);

            // [REFINE] Apply theme setting immediately to prevent flicker from gray to dark.
            // ウインドウが表示される前にテーマを適用し、灰色からのチラつきを防止します。
            UpdateTheme(config.System.BaseTheme);
            ObserveThemeTrigger();

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
                if (File.Exists(triggerFile))
                {
                    try
                    {
                        var theme = File.ReadAllText(triggerFile).Trim();
                        File.Delete(triggerFile);
                        
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
                HandleDeviceCleanup(cashChanger);
            }
            catch { /* Ignore if device was not initialized or failure during resolve */ }
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

    public static void UpdateTheme(string themeName)
    {
        if (Current == null) return;

        try
        {
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var isActuallyDark = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            
            // 1. Apply Base Material Design Theme
            var theme = paletteHelper.GetTheme();
            var baseTheme = isActuallyDark 
                ? MaterialDesignThemes.Wpf.BaseTheme.Dark 
                : MaterialDesignThemes.Wpf.BaseTheme.Light;
            MaterialDesignThemes.Wpf.ThemeExtensions.SetBaseTheme(theme, baseTheme);

            if (isActuallyDark)
            {
                // [RESTORE] Soft Dark primary colors
                MaterialDesignThemes.Wpf.ThemeExtensions.SetPrimaryColor(theme, Color.FromRgb(0xBB, 0x86, 0xFC));
                MaterialDesignThemes.Wpf.ThemeExtensions.SetSecondaryColor(theme, Color.FromRgb(0x03, 0xDA, 0xC6));
                theme.Background = Color.FromRgb(0x12, 0x12, 0x12);
            }
            paletteHelper.SetTheme(theme);

            // 2. Load and Apply Project-Specific Theme Resources (XAML)
            var resourceName = isActuallyDark ? "Colors.Dark.xaml" : "Colors.Light.xaml";
            var themeDict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/CashChangerSimulator.UI.Wpf;component/Themes/{resourceName}")
            };

            var dictionaries = Current.Resources.MergedDictionaries;
            bool replaced = false;
            for (int i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.ToString() ?? "";
                if (source.Contains("Colors.Light.xaml") || source.Contains("Colors.Dark.xaml"))
                {
                    dictionaries[i] = themeDict;
                    replaced = true;
                    break;
                }
            }
            if (!replaced) dictionaries.Add(themeDict);

            // 3. Map XAML resources to MD3 consistent keys for runtime stability
            // XAML から読み込まれたキーを、動的な MaterialDesign ブラシやプロジェクト共通キーにマッピングします。
            var bg = (Color)Current.Resources["BackgroundColor"];
            var fg = (Color)Current.Resources["OnSurfaceColor"];
            var primary = (Color)Current.Resources["PrimaryColor"];
            var secondary = (Color)Current.Resources["SecondaryColor"];
            var surface = (Color)Current.Resources["SurfaceColor"];
            var surfaceVariant = (Color)Current.Resources["SurfaceVariantColor"];
            var surfaceContainerHigh = (Color)Current.Resources["SurfaceContainerHighColor"];
            var onSurfaceVariant = (Color)Current.Resources["OnSurfaceVariantColor"];
            var outline = (Color)Current.Resources["OutlineColor"];
            var warningFg = (Color)Current.Resources["WarningForegroundColor"];
            var inventoryTileBg = (Color)Current.Resources["InventoryTileBackgroundColor"];

            var bgBrush = new SolidColorBrush(bg); bgBrush.Freeze();
            var fgBrush = new SolidColorBrush(fg); fgBrush.Freeze();
            var primaryBrush = new SolidColorBrush(primary); primaryBrush.Freeze();
            var secondaryBrush = new SolidColorBrush(secondary); secondaryBrush.Freeze();
            var surfaceBrush = new SolidColorBrush(surface); surfaceBrush.Freeze();
            var surfaceVariantBrush = new SolidColorBrush(surfaceVariant); surfaceVariantBrush.Freeze();
            var surfaceContainerHighBrush = new SolidColorBrush(surfaceContainerHigh); surfaceContainerHighBrush.Freeze();
            var onSurfaceVariantBrush = new SolidColorBrush(onSurfaceVariant); onSurfaceVariantBrush.Freeze();
            var outlineBrush = new SolidColorBrush(outline); outlineBrush.Freeze();
            var warningFgBrush = new SolidColorBrush(warningFg); warningFgBrush.Freeze();
            var inventoryTileBgBrush = new SolidColorBrush(inventoryTileBg); inventoryTileBgBrush.Freeze();

            Current.Resources["MaterialDesign.Brush.Background"] = bgBrush;
            Current.Resources["MaterialDesign.Brush.OnBackground"] = fgBrush;
            Current.Resources["MaterialDesign.Brush.Surface"] = surfaceBrush;
            Current.Resources["MaterialDesign.Brush.OnSurface"] = fgBrush;
            Current.Resources["MaterialDesign.Brush.SurfaceVariant"] = surfaceVariantBrush;
            Current.Resources["MaterialDesign.Brush.OnSurfaceVariant"] = onSurfaceVariantBrush;
            Current.Resources["MaterialDesign.Brush.Primary"] = primaryBrush;
            Current.Resources["MaterialDesign.Brush.Secondary"] = secondaryBrush;
            Current.Resources["MaterialDesign.Brush.SurfaceContainerHigh"] = surfaceContainerHighBrush;
            Current.Resources["MaterialDesign.Brush.Outline"] = outlineBrush;
            
            Current.Resources["MaterialDesignBody"] = fgBrush;
            Current.Resources["MaterialDesignPaper"] = bgBrush;
            Current.Resources["WarningForegroundBrush"] = warningFgBrush;

            // Compat keys for StaticResource/DynamicResource in older/shared templates
            Current.Resources["PrimaryColor"] = primary;
            Current.Resources["SecondaryColor"] = secondary;
            Current.Resources["BackgroundColor"] = bg;
            Current.Resources["SurfaceColor"] = surface;
            Current.Resources["OnSurfaceColor"] = fg;

            // Semantic Brushes
            Current.Resources["App.Brush.Foreground"] = fgBrush;
            Current.Resources["App.Brush.Primary"] = primaryBrush;
            Current.Resources["App.Brush.InventoryTile.Background"] = inventoryTileBgBrush;
        }
        catch (Exception ex)
        {
            LogProvider.CreateLogger<App>().LogError(ex, "Failed to update theme to {Theme}", themeName);
        }
    }

    private void HandleFatalException(Exception? ex)
    {
        if (ex == null) return;

        try
        {
            Console.Error.WriteLine($"[FATAL_APP_CRASH] {ex}");
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FATAL_CRASH.txt");
            File.WriteAllText(logPath, $"[FATAL] [{DateTime.Now}] {ex}");
        }
        catch { }
        
        try
        {
            var cashChanger = DIContainer.Resolve<SimulatorCashChanger>();
            if (cashChanger != null)
            {
                HandleDeviceCleanup(cashChanger);
            }
        }
        catch { }
    }

    /// <summary>デバイスの安全な終了シーケンス（Disable -> Release -> Close）を実行します。</summary>
    private void HandleDeviceCleanup(SimulatorCashChanger cashChanger)
    {
        try
        {
            if (cashChanger.State == Microsoft.PointOfService.ControlState.Closed) return;

            // [SAFE SEQUENCE] Ensure the device follows Disable -> Release -> Close.
            // These calls use the internal hardening we added to StandardLifecycleHandler.
            if (cashChanger.DeviceEnabled) cashChanger.DeviceEnabled = false;
            // Claimed/Release/Close are handled inside Close() via our hardened StandardLifecycleHandler.
            cashChanger.Close();
            
            var logger = LogProvider.CreateLogger<App>();
            logger.LogInformation("Device cleanup successful during shutdown.");
        }
        catch (Exception ex)
        {
            var logger = LogProvider.CreateLogger<App>();
            logger.LogWarning(ex, "Device cleanup failed during shutdown.");
        }
    }
}

