using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using System.IO;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Documentation;

/// <summary>
/// FlaUI を使用して、ドキュメント用の高品質なスクリーンショットを自動生成するテストクラス。
/// </summary>
public class DocumentationScreenshotGenerator : IDisposable
{
    private readonly CashChangerTestApp _app;
    private readonly string _outputDir;

    public DocumentationScreenshotGenerator()
    {
        _app = new CashChangerTestApp();
        _app.Launch();

        // docs/images フォルダを特定
        // 実行ディレクトリ (test/bin/Debug/...) からリポジトリルートの docs/images へ
        _outputDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../docs/images"));
        if (!Directory.Exists(_outputDir))
        {
            Directory.CreateDirectory(_outputDir);
        }
    }

    /// <summary>
    /// 主要な画面（メイン、入金、払出、高度なシミュレーション）を順に開き、キャプチャを保存する。
    /// </summary>
    [Fact]
    public void GenerateScreenshots()
    {
        // 1. メインダッシュボードのキャプチャ
        Thread.Sleep(3000);
        _app.MainWindow.ShouldNotBeNull();
        CaptureElement(_app.MainWindow, "main_dashboard.png");

        // 2. 入金画面のキャプチャ
        CaptureSubWindow("LaunchDepositButton", "DepositWindow", "deposit_window.png");

        // 3. 払出画面のキャプチャ
        CaptureSubWindow("LaunchDispenseButton", "DispenseWindow", "dispense_window.png");

        // 4. 高度なシミュレーション画面のキャプチャ
        CaptureSubWindow("LaunchAdvancedSimulationButton", "AdvancedSimulationWindow", "advanced_simulation.png");

        // 5. 金種詳細ダイアログのキャプチャ
        CaptureDenominationDetail("InventoryTile", "DenominationDetailDialogView", "inventory_detail.png");
    }

    private void CaptureDenominationDetail(string tileId, string dialogId, string fileName)
    {
        var mainWindow = _app?.MainWindow;
        if (mainWindow == null) throw new Exception("MainWindow is not available.");

        var tile = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(tileId))?.AsButton()
                   ?? throw new Exception("Inventory tile not found.");
        // タイトルをクリックする前に、状態を確認
        tile.WaitUntilEnabled(TimeSpan.FromSeconds(5));
        tile.WaitUntilClickable(TimeSpan.FromSeconds(5));
        
        Console.WriteLine($"Aggressive clicking tile: {tile.Name}, AutomationId: {tile.AutomationId}");

        // 1. フォーカスを当てる
        tile.Focus();
        Thread.Sleep(500);

        // 2. Invoke パターンを試行
        if (tile.Patterns.Invoke.IsSupported)
        {
            try { tile.Patterns.Invoke.Pattern.Invoke(); } catch { }
        }
        
        // 3. マウスクリックを試行
        try { tile.Click(false); } catch { }
        
        // 4. キーボード Enter を試行
        try { FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN); } catch { }

        // ダイアログが表示されるのを待機
        var dialog = Retry.WhileNull(() =>
        {
            if (_app == null) return null;
            // A. MainWindow の子孫
            var found = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
            if (found != null) return found;

            // B. Desktop 直下の全要素から再帰的に検索
            var automation = _app.Automation;
            if (automation == null) return null;
            var desktop = automation.GetDesktop();
            foreach (var child in desktop.FindAllChildren())
            {
                var inChild = child.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
                if (inChild != null) return inChild;
            }

            return null;
        }, TimeSpan.FromSeconds(25)).Result;

        if (dialog == null)
        {
            throw new Exception($"Denomination detail dialog not found (ID: {dialogId}).");
        }

        Thread.Sleep(2000); // 描画・アニメーション待ち
        CaptureElement(dialog, fileName);

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

    private void CaptureSubWindow(string buttonId, string windowId, string fileName)
    {
        // AutomationId で見つからない場合のフォールバックとして Name でも試行
        var button =
            (_app.MainWindow
            ?.FindFirstDescendant(cf => cf.ByAutomationId(buttonId))
            ?.AsButton()
            ?? _app.MainWindow
                ?.FindFirstDescendant(
                    cf => cf
                        .ByName(buttonId.Replace("Launch", "")))
                ?.AsButton())
                ?? throw new Exception($"Button '{buttonId}' not found in MainWindow.");
        
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

        // ウィンドウが開くのを待機
        var window = Retry.WhileNull(() =>
        {
            // Windows は Desktop 直下であることが多いため、まず Desktop を確認
            var desktop = _app.Automation.GetDesktop();
            var win = desktop.FindFirstChild(cf => cf.ByAutomationId(windowId));
            if (win != null) return win.AsWindow();

            // 見つからない場合は MainWindow の子孫（埋め込みウィンドウなど）を確認
            if (_app.MainWindow != null)
            {
                win = _app.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(windowId));
                if (win != null) return win.AsWindow();
            }

            // 名前でのフォールバック
            win = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                    .FirstOrDefault(w => w.Name != null && (w.Name.Contains("TERMINAL") || w.Name.Contains("Simulation") || w.Name.Contains("Controls")));

            return win?.AsWindow();
        }, TimeSpan.FromSeconds(20)).Result
        ?? throw new Exception($"Window '{windowId}' not found after clicking '{buttonId}'.");

        window.SetForeground();
        Thread.Sleep(2000);
        CaptureElement(window, fileName);

        window.Close();
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
        _app?.Dispose();
        GC.SuppressFinalize(this);
    }
}
