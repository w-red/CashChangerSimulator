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

            // Project Standard Colors - Restoring the "Soft Dark" palette as requested
            // Light Theme: Material Design Standard
            // Dark Theme: "Soft Dark" (Original Solution)
            if (themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                // [RESTORE] Restore "True Original" colors from commit 1c72d1cc
                // Background: #121212, Primary: #BB86FC, Secondary: #03DAC6, OnSurface: #E0E0E0
                var darkTheme = MaterialDesignThemes.Wpf.Theme.Create(MaterialDesignThemes.Wpf.BaseTheme.Dark, 
                    System.Windows.Media.Color.FromRgb(0xBB, 0x86, 0xFC), 
                    System.Windows.Media.Color.FromRgb(0x03, 0xDA, 0xC6));

                darkTheme.Background = System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x12);
                
                paletteHelper.SetTheme(darkTheme);
            }
            else
            {
                var currentTheme = paletteHelper.GetTheme();
                MaterialDesignThemes.Wpf.ThemeExtensions.SetBaseTheme(currentTheme, MaterialDesignThemes.Wpf.BaseTheme.Light);
                paletteHelper.SetTheme(currentTheme);
            }
            
            // Define colors based on the current theme state
            // Use fixed values for stability across library versions when possible
            bool isActuallyDark = themeName.Equals("Dark", StringComparison.OrdinalIgnoreCase);
            
            var bg = isActuallyDark ? System.Windows.Media.Color.FromRgb(0x12, 0x12, 0x12) : System.Windows.Media.Color.FromRgb(0xF5, 0xF5, 0xF5);
            var fg = isActuallyDark ? System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0) : System.Windows.Media.Color.FromRgb(0x00, 0x00, 0x00);
            var surface = isActuallyDark ? System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E) : System.Windows.Media.Colors.White;
            var onSurfaceVariant = isActuallyDark ? System.Windows.Media.Color.FromRgb(0xA0, 0xA0, 0xA0) : System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44);

            var currentThemeColors = paletteHelper.GetTheme();
            var primary = currentThemeColors.PrimaryMid.Color;
            var secondary = currentThemeColors.SecondaryMid.Color;

            // Force override specific brushes to GUARANTEE visibility and theme consistency
            var bgBrush = new System.Windows.Media.SolidColorBrush(bg); bgBrush.Freeze();
            var fgBrush = new System.Windows.Media.SolidColorBrush(fg); fgBrush.Freeze();
            var primaryBrush = new System.Windows.Media.SolidColorBrush(primary); primaryBrush.Freeze();
            var secondaryBrush = new System.Windows.Media.SolidColorBrush(secondary); secondaryBrush.Freeze();
            var variantBrush = new System.Windows.Media.SolidColorBrush(onSurfaceVariant); variantBrush.Freeze();

            // Material Design standard brushes
            Current.Resources["MaterialDesign.Brush.Background"] = bgBrush;
            Current.Resources["MaterialDesign.Brush.OnBackground"] = fgBrush;
            Current.Resources["MaterialDesign.Brush.Surface"] = new System.Windows.Media.SolidColorBrush(surface);
            Current.Resources["MaterialDesign.Brush.OnSurface"] = fgBrush;
            Current.Resources["MaterialDesign.Brush.OnSurfaceVariant"] = variantBrush;
            Current.Resources["MaterialDesign.Brush.Primary"] = primaryBrush;
            Current.Resources["MaterialDesign.Brush.OnPrimary"] = new System.Windows.Media.SolidColorBrush(isLight ? System.Windows.Media.Colors.White : System.Windows.Media.Color.FromRgb(0x1A, 0x00, 0x33));
            Current.Resources["MaterialDesign.Brush.Secondary"] = secondaryBrush;
            Current.Resources["MaterialDesign.Brush.OnSecondary"] = new System.Windows.Media.SolidColorBrush(isLight ? System.Windows.Media.Colors.Black : System.Windows.Media.Color.FromRgb(0x00, 0x33, 0x0E));
            Current.Resources["MaterialDesign.Brush.OnTertiary"] = new System.Windows.Media.SolidColorBrush(isLight ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black);

            // [STABILITY] Force Body/Paper brushes which are often used as default text/bg
            Current.Resources["MaterialDesignBody"] = fgBrush;
            Current.Resources["MaterialDesignPaper"] = bgBrush;
            Current.Resources["MaterialDesignSelection"] = primaryBrush;
            Current.Resources["MaterialDesignColumnHeader"] = fgBrush;
            Current.Resources["MaterialDesignDivider"] = variantBrush;

            // Compat and project-specific keys
            Current.Resources["BackgroundColor"] = bg;
            Current.Resources["PrimaryColor"] = primary;
            Current.Resources["SecondaryColor"] = secondary;
            Current.Resources["SurfaceColor"] = surface;
            Current.Resources["OnSurfaceColor"] = fg;
            Current.Resources["CardShadowOpacity"] = isLight ? 0.05 : 0.4;

            // App specific semantic brushes
            Current.Resources["App.Brush.Foreground"] = fgBrush;
            Current.Resources["App.Brush.Primary"] = primaryBrush;
            Current.Resources["WarningBrush"] = secondaryBrush;
            Current.Resources["TextPrimaryBrush"] = fgBrush;
            Current.Resources["PrimaryBrush"] = primaryBrush;
            Current.Resources["SecondaryBrush"] = secondaryBrush;
            
            // Explicitly set for Advanced Simulation UI consistency
            Current.Resources["MaterialDesign.Brush.SurfaceVariant"] = new System.Windows.Media.SolidColorBrush(isActuallyDark ? System.Windows.Media.Color.FromRgb(0x2C, 0x2C, 0x2C) : System.Windows.Media.Color.FromRgb(0xEF, 0xEF, 0xEF));

            // [RESTORE] Dedicated brushes for the "CLOSE" button to ensure visibility
            // Light: Gray background with Black text
            // Dark: Dark gray background with White text
            var closeBg = isActuallyDark ? System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33) : System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0);
            var closeFg = isActuallyDark ? System.Windows.Media.Colors.White : System.Windows.Media.Colors.Black;
            Current.Resources["App.Brush.Close.Background"] = new System.Windows.Media.SolidColorBrush(closeBg);
            Current.Resources["App.Brush.Close.Foreground"] = new System.Windows.Media.SolidColorBrush(closeFg);
        }
        catch (Exception ex)
        {
            LogProvider.CreateLogger<App>().LogError(ex, "Failed to update theme to {Theme}", themeName);
        }
    }
}

