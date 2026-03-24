using CashChangerSimulator.UI.Tests;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using System.IO;
using Shouldly;


namespace CashChangerSimulator.UI.DocumentationTests;

/// <summary>
/// FlaUI を使用して、ドキュメント用の高品質なスクリーンショットを自動生成するテストクラス。
/// </summary>
[Collection("SequentialTests")]
[Trait("Category", "ManualOnly")] // CIや一括テスト実行時には除外するためのカテゴリ
public class DocumentationScreenshotGenerator : IDisposable
{
    private readonly CashChangerTestApp _app;
    private readonly string _outputDir;

    public DocumentationScreenshotGenerator()
    {
        _app = new CashChangerTestApp();
        var dialogTriggerPath = Path.GetFullPath("open_dialog.trigger");
        var themeTriggerPath = Path.GetFullPath("theme.trigger");

        _app.Launch(envVars: new Dictionary<string, string> 
        { 
            { "TEST_AUTO_OPEN_INVENTORY_DIALOG_FILE", dialogTriggerPath },
            { "TEST_AUTO_CHANGE_THEME_FILE", themeTriggerPath }
        });

        // docs/images フォルダを特定
        _outputDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../docs/images"));
        if (!Directory.Exists(_outputDir))
        {
            Directory.CreateDirectory(_outputDir);
        }
    }

    /// <summary>
    /// 主要な画面をライトテーマでキャプチャする。
    /// </summary>
    [Fact]
    public void GenerateLightModeScreenshots()
    {
        CaptureScreenshotsForTheme("Light");
    }

    /// <summary>
    /// 主要な画面をダークテーマでキャプチャする。
    /// </summary>
    [Fact]
    public void GenerateDarkModeScreenshots()
    {
        CaptureScreenshotsForTheme("Dark");
    }

    private void CaptureScreenshotsForTheme(string theme)
    {
        SetTheme(theme);
        var themeDir = Path.Combine(_outputDir, theme);
        if (!Directory.Exists(themeDir)) Directory.CreateDirectory(themeDir);

        // 1. メインダッシュボードのキャプチャ
        Thread.Sleep(5000); // 待機時間を延長 (3s -> 5s)
        
        // [STABILITY] Get a fresh reference to the MainWindow if needed
        var window = Retry.WhileNull(() => {
            var win = _app.MainWindow;
            if (win != null && !win.IsOffscreen) return win;
            return null;
        }, TimeSpan.FromSeconds(15)).Result;

        window.ShouldNotBeNull("MainWindow is not available or offscreen.");
        CaptureElement(window, Path.Combine(theme, "main_dashboard.png"));

        // 2. 入金画面のキャプチャ
        CaptureSubWindow("LaunchDepositButton", "DepositWindow", Path.Combine(theme, "deposit_window.png"));

        // 3. 払出画面のキャプチャ
        CaptureSubWindow("LaunchDispenseButton", "DispenseWindow", Path.Combine(theme, "dispense_window.png"));

        // 4. 高度なシミュレーション画面のキャプチャ (エラー状態を誘発)
        CaptureSubWindow("LaunchAdvancedSimulationButton", "AdvancedSimulationWindow", Path.Combine(theme, "advanced_simulation.png"), (win) => {
            try 
            {
                var jamBtn = win.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton"))?.AsButton();
                var overlapBtn = win.FindFirstDescendant(cf => cf.ByAutomationId("SimulateOverlapButton"))?.AsButton();
                
                if (jamBtn != null) { jamBtn.WaitUntilClickable(TimeSpan.FromSeconds(2)); jamBtn.Click(); }
                if (overlapBtn != null) { overlapBtn.WaitUntilClickable(TimeSpan.FromSeconds(2)); overlapBtn.Click(); }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Failed to trigger jam/overlap simulation: {ex.Message}");
            }
            
            Thread.Sleep(1500); // 警告インジケーター表示待ち
        });

        // 5. 金種詳細ダイアログのキャプチャ
        CaptureDenominationDetail("InventoryTile", "DenominationDetailDialogView", Path.Combine(theme, "inventory_detail.png"));
    }

    private void SetTheme(string theme)
    {
        Console.WriteLine($"Setting theme to: {theme}");
        File.WriteAllText(Path.GetFullPath("theme.trigger"), theme);
        Thread.Sleep(5000); // テーマ適用待ち
    }

    private void CaptureDenominationDetail(string tileId, string dialogId, string fileName)
    {
        var mainWindow = _app?.MainWindow;
        if (mainWindow == null) throw new Exception("MainWindow is not available.");

        // The View Model will auto-open this dialog when the trigger file is detected.
        Console.WriteLine("Writing open_dialog.trigger to trigger ViewModel hook...");
        System.IO.File.WriteAllText(Path.GetFullPath("open_dialog.trigger"), "open");

        AutomationElement? dialog = null;

        // ダイアログが表示されるのを待機 (待機時間を 30 秒に延長)
        dialog = Retry.WhileNull(() =>
        {
            var win = _app!.MainWindow;
            if (win == null) return null;
            
            // A. MainWindow の子孫
            var found = win.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
            if (found != null && !found.IsOffscreen) return found;

            // B. Desktop 直下の全要素から再帰的に検索
            var desktop = _app.Automation.GetDesktop();
            var appWindows = desktop.FindAllChildren(cf => cf.ByProcessId(_app.Application.ProcessId));
            foreach (var appWin in appWindows)
            {
                var inWin = appWin.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
                if (inWin != null && !inWin.IsOffscreen) return inWin;
            }

            // C. 名前、あるいはクラス名でフォールバック検索
            var fallback = desktop.FindFirstDescendant(cf => cf.ByClassName("Popup").Or(cf.ByClassName("DialogHost")).Or(cf.ByName("Denomination Detail")));
            if (fallback != null && !fallback.IsOffscreen) return fallback;

            return null;
        }, TimeSpan.FromSeconds(30)).Result;

        if (dialog == null)
        {
            Console.WriteLine($"[ERROR] Denomination detail dialog '{dialogId}' not found. Skipping.");
            return;
        }

        Thread.Sleep(2000); // 描画・アニメーション待ち
        try 
        {
            CaptureElement(dialog, fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to capture dialog {fileName}: {ex.Message}");
        }

        // ダイアログを閉じる
        var closeButton = dialog.FindFirstDescendant(cf => cf.ByName("Close").Or(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))?.AsButton();
        if (closeButton != null)
        {
            if (closeButton.Patterns.Invoke.IsSupported) closeButton.Patterns.Invoke.Pattern.Invoke();
            else closeButton.Click();
        }
        else
        {
            if (mainWindow != null) mainWindow.Focus();
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
            // After RETURN, wait a bit and escape
            Thread.Sleep(500);
            FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ESCAPE);
        }
        Thread.Sleep(1000);
    }

    private void CaptureSubWindow(string buttonId, string windowId, string fileName, Action<Window>? action = null)
    {
        // Use Retry for button because UI might be slow to load fully
        var button = Retry.WhileNull(() =>
        {
            var win = _app.MainWindow;
            if (win == null) return null;
            
            var btn = win.FindFirstDescendant(cf => cf.ByAutomationId(buttonId))?.AsButton();
            if (btn != null && btn.IsEnabled && !btn.IsOffscreen) return btn;
            
            // Fallback: Name search
            btn = win.FindFirstDescendant(cf => cf.ByName(buttonId.Replace("Launch", "")))?.AsButton();
            if (btn != null && btn.IsEnabled && !btn.IsOffscreen) return btn;
            
            return null;
        }, TimeSpan.FromSeconds(20)).Result;

        if (button == null) throw new Exception($"Button '{buttonId}' not found or not ready in MainWindow.");
        
        button.WaitUntilEnabled(TimeSpan.FromSeconds(5));
        button.WaitUntilClickable(TimeSpan.FromSeconds(5));

        if (button.Patterns.Invoke.IsSupported)
        {
            button.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            button.Click();
        }

        // ウィンドウが開くのを待機 (自前ループに置き換えてタイムアウトを厳密に制御)
        AutomationElement? windowRaw = null;
        for (int i = 0; i < 10; i++) // 最大10秒 (1s * 10)
        {
            var desktop = _app.Automation.GetDesktop();
            var win = desktop.FindFirstChild(cf => cf.ByAutomationId(windowId));
            if (win == null && _app.MainWindow != null)
            {
                win = _app.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(windowId));
            }
            if (win == null)
            {
                win = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                        .FirstOrDefault(w => w.Name != null && (w.Name.Contains("TERMINAL") || w.Name.Contains("Simulation") || w.Name.Contains("Controls") || w.Name.Contains("Advanced")));
            }

            if (win != null)
            {
                windowRaw = win;
                break;
            }
            Thread.Sleep(1000);
        }

        if (windowRaw == null)
        {
            Console.WriteLine($"[ERROR] Window '{windowId}' not found after 10 attempts. Skipping this capture.");
            return;
        }

        var window = windowRaw.AsWindow();

        window.SetForeground();
        Thread.Sleep(2000);

        try 
        {
            // プレキャプチャアクション（エラーの意図的な発生など）
            action?.Invoke(window);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Pre-capture action failed for {windowId}: {ex.Message}");
            // Continue even if action fails to at least try to capture something
        }

        try 
        {
            CaptureElement(window, fileName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to capture {fileName}: {ex.Message}");
        }

        try 
        {
            // [STABILITY] Explicitly find even if off-screen or not immediately visible
            AutomationElement? resetBtnRaw = null;
            try { resetBtnRaw = window.FindFirstDescendant(cf => cf.ByAutomationId("ResetErrorButton")); } catch { }
            
            if (resetBtnRaw != null)
            {
                var resetBtn = resetBtnRaw.AsButton();
                Console.WriteLine($"[DEBUG] ResetErrorButton found. IsEnabled={resetBtn.IsEnabled}");
                
                if (resetBtn.IsEnabled)
                {
                    try 
                    {
                        if (resetBtn.Patterns.Invoke.IsSupported) resetBtn.Patterns.Invoke.Pattern.Invoke();
                        else resetBtn.Click();
                    }
                    catch (Exception exInner)
                    {
                        Console.WriteLine($"[DEBUG] Invoke failed for ResetErrorButton: {exInner.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to process reset state (non-critical): {ex.Message}");
        }

        try 
        {
            window.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to close window (non-critical): {ex.Message}");
        }
        Thread.Sleep(1000);
    }

    private void CaptureElement(AutomationElement element, string fileName)
    {
        // 要素のみをキャプチャ（デスクトップ背景を含まない）
        using var image = Capture.Element(element);
        var path = Path.Combine(_outputDir, fileName);
        image.ToFile(path);
        Console.WriteLine($"Screenshot saved to: {path}");
    }

    public void Dispose()
    {
        try { _app?.Dispose(); } catch { }
        
        // Ensure the process is definitely gone to prevent DLL entry locking
        try
        {
            var processName = "CashChangerSimulator.UI.Wpf";
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var p in processes)
            {
                try { p.Kill(true); } catch { }
            }
        }
        catch { }

        GC.SuppressFinalize(this);
    }
}
