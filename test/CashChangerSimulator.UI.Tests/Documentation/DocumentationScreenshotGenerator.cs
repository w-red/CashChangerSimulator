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
        var triggerPath = Path.GetFullPath("open_dialog.trigger");
        _app.Launch(envVars: new Dictionary<string, string> { { "TEST_AUTO_OPEN_INVENTORY_DIALOG_FILE", triggerPath } });

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

        // The View Model will auto-open this dialog when the trigger file is detected.
        Console.WriteLine("Writing open_dialog.trigger to trigger ViewModel hook...");
        System.IO.File.WriteAllText(Path.GetFullPath("open_dialog.trigger"), "open");

        AutomationElement? dialog = null;

            // ダイアログが表示されるのを待機
            dialog = Retry.WhileNull(() =>
            {
                if (_app == null) return null;
                // A. MainWindow の子孫 (DialogHost は MainWindow 上にオーバーレイされる)
                var found = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
                if (found != null && !found.IsOffscreen) return found;

                // C. 名前、あるいはクラス名でフォールバック検索（MaterialDesignThemes DialogHost への対策）
                found = _app.MainWindow?.FindFirstDescendant(cf => cf.ByClassName("Popup").Or(cf.ByClassName("DialogHost")).Or(cf.ByName("Denomination Detail")));
                if (found != null && !found.IsOffscreen)
                {
                   // 中に目的のダイアログがあるか確認
                   var inner = found.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
                   if (inner != null && !inner.IsOffscreen) return inner;
                   return found; // 見つからなくてもオーバーレイ自体を返す
                }

                // B. Desktop 直下の全要素から再帰的に検索
                var automation = _app.Automation;
                if (automation == null) return null;
                var desktop = automation.GetDesktop();
                foreach (var child in desktop.FindAllChildren())
                {
                    var inChild = child.FindFirstDescendant(cf => cf.ByAutomationId(dialogId));
                    if (inChild != null && !inChild.IsOffscreen) return inChild;
                }

                return null;
            }, TimeSpan.FromSeconds(10)).Result;

        if (dialog == null)
        {
            throw new Exception($"Denomination detail dialog not found (ID: {dialogId}) after 3 attempts.");
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
