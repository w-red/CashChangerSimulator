using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Tools;
using System.IO;

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
        Assert.NotNull(_app.MainWindow);
        CaptureElement(_app.MainWindow, "main_dashboard.png");

        // 2. 入金画面のキャプチャ
        CaptureSubWindow("LaunchDepositButton", "DepositWindow", "deposit_window.png");

        // 3. 払出画面のキャプチャ
        CaptureSubWindow("LaunchDispenseButton", "DispenseWindow", "dispense_window.png");

        // 4. 高度なシミュレーション画面のキャプチャ
        CaptureSubWindow("LaunchAdvancedSimulationButton", "AdvancedSimulationWindow", "advanced_simulation.png");
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
        button.WaitUntilClickable();
        button.Click();
        
        // ウィンドウが開くのを待機 (Desktop 直下 または MainWindow の子)
        var window = Retry.WhileNull(() => 
        {
            var desktop = _app.Automation.GetDesktop();
            var win = 
                desktop.FindFirstChild(
                    cf => cf.ByAutomationId(windowId)) 
                ?? _app.MainWindow
                    ?.FindFirstDescendant(
                        cf => cf.ByAutomationId(windowId));
            
            if (win != null) return win.AsWindow();

            // 名前でのフォールバック
            win = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                    .FirstOrDefault(w => w.Name != null && (w.Name.Contains("TERMINAL") || w.Name.Contains("Simulation") || w.Name.Contains("Controls")));
            
            return win?.AsWindow();
        }, TimeSpan.FromSeconds(10)).Result
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
