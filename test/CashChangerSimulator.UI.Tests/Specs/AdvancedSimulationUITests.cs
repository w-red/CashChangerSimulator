using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>高度なシミュレーション機能（エラー発生と解消など）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class AdvancedSimulationUITests : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリのフィクスチャを受け取り、初期状態をセットアップする。</summary>
    public AdvancedSimulationUITests(CashChangerTestApp app)
    {
        _app = app;
    }

    /// <summary>エラーインジケータがエラー発生時に表示され、リセット後に消えることを検証する。</summary>
    /// <remarks>
    /// 以下のステップを実行します：
    /// 1. 高度なシミュレーションウィンドウを開く
    /// 2. ジャム（エラー）をシミュレートする
    /// 3. エラー表示を確認する
    /// 4. エラーをリセットして表示が消えることを確認する
    /// </remarks>
    [Fact]
    public void ErrorIndicatorsShouldBeVisibleWhenErrorExists()
    {
        // Arrange
        _app.Launch();
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");

        // デバッグ: UIツリーを再帰的にダンプする
        var allDescendants = window.FindAllDescendants();
        var tree = string.Join(", ", allDescendants.Select(d => 
        {
            var n = "Unknown";
            var id = "Unknown";
            var enabled = false;
            try { n = d.Name; } catch { }
            try { id = d.AutomationId; } catch { }
            try { enabled = d.IsEnabled; } catch { }
            return $"'{n}' ({id}) [Enabled={enabled}]";
        }));
        // throw new Exception($@"Full UI Tree: {tree}"); // 必要に応じて有効化

        // デバイスが接続されていない場合は接続する (Open)
        var openBtn = window.FindFirstDescendant("DeviceOpenButton");
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            var btn = openBtn.AsButton();
            btn.ShouldNotBeNull();
            if (btn!.Patterns.Invoke.IsSupported) btn.Patterns.Invoke.Pattern.Invoke();
            else btn.SmartClick();
        }

        // 接続完了まで待機
        var closeBtn = Retry.WhileNull(() => window.FindFirstDescendant("DeviceCloseButton"), TimeSpan.FromSeconds(15)).Result;
        closeBtn.ShouldNotBeNull("DeviceCloseButton not found.");
        closeBtn.WaitUntilEnabled(TimeSpan.FromSeconds(15));

        // Advanced Simulation ウィンドウを開く
        var launchBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchAdvancedSimulationButton"));
        if (launchBtn == null)
        {
            var sb = new System.Text.StringBuilder();
            CaptureElements(window, 0, sb);
            throw new Exception($"LaunchAdvancedSimulationButton not found. MainWindow Tree:\n{sb}");
        }
        
        // Ensure the button is ready
        Retry.WhileFalse(() => launchBtn.IsEnabled, TimeSpan.FromSeconds(10));

        try { launchBtn.Focus(); } catch { /* focus is optional; proceed if it fails */ }
        Thread.Sleep(500);
        
        // Ensure it's clicked. Try multiple ways if needed.
        var launchBtnAsButton = launchBtn.AsButton();
        launchBtnAsButton.ShouldNotBeNull();
        if (launchBtnAsButton!.Patterns.Invoke.IsSupported) launchBtnAsButton.Patterns.Invoke.Pattern.Invoke();
        else launchBtnAsButton.SmartClick();
        // ウィンドウを検索 (堅牢な共通ロジックを使用)
        var advWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "AdvancedSimulationWindow", timeout: UITestTimings.RetryLongTimeout);
        advWindow.ShouldNotBeNull("Advanced Simulation window should be open.");
        advWindow.SetForeground();

        // 在庫の読み込み（InventoryTileの描画）を待つための診断
        var inventoryFound = Retry.WhileFalse(() => {
            var tiles = window.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile"));
            if (tiles.Length > 0) return true;
            Console.WriteLine("[DIAG] Waiting for InventoryTiles to appear...");
            return false;
        }, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(2)).Success;

        if (!inventoryFound)
        {
            Console.WriteLine("[WARNING] InventoryTiles did not appear within timeout.");
        }

        var simulateJamBtnResult = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("AdvancedSimulateJamButton")), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(2));
        var simulateJamBtn = simulateJamBtnResult.Result?.AsButton();

        if (simulateJamBtn == null)
        {
            Console.WriteLine("[DIAG] SimulateJamButton NOT FOUND in AdvancedSimulationWindow. Dumping tree:");
            var dump = new System.Text.StringBuilder();
            CaptureElements(advWindow, 0, dump);
            Console.WriteLine(dump.ToString());
            throw new Exception($"AdvancedSimulateJamButton not found in AdvancedSimulationWindow. Tree:\n{dump}");
        }

        // Act: Simulate Jam
        simulateJamBtn!.Focus();
        if (simulateJamBtn.Patterns.Invoke.IsSupported) simulateJamBtn.Patterns.Invoke.Pattern.Invoke();
        else simulateJamBtn.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        var jamIndicator = Retry.WhileNull(() => 
        {
            var el = advWindow.FindFirstDescendant(cf => cf.ByAutomationId("JamIndicatorText"));
            if (el != null && !el.IsOffscreen) return el;
            return null;
        }, UITestTimings.RetryLongTimeout).Result;
        
        jamIndicator.ShouldNotBeNull("JamIndicator should be visible after simulation");

        // ResetErrorButton は常にツリーに配置されているが、エラー発生後にのみ Enabled となる
        var resetBtn = advWindow.FindFirstDescendant(cf => cf.ByAutomationId("ResetErrorButton"))?.AsButton();
        resetBtn.ShouldNotBeNull("ResetErrorButton not found");
        
        Retry.WhileFalse(() => resetBtn.IsEnabled, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("ResetErrorButton should enable after jam");

        // Act: Reset
        if (resetBtn!.Patterns.Invoke.IsSupported) resetBtn.Patterns.Invoke.Pattern.Invoke();
        else resetBtn.SmartClick();

        // Assert: Jam indicator should disappear (Visibility=Collapsed)
        Retry.WhileFalse(() => {
            var indicator = advWindow.FindFirstDescendant(cf => cf.ByAutomationId("JamIndicatorText"));
            return indicator == null || indicator.IsOffscreen;
        }, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("JamIndicator should be hidden after reset");

        // Cleanup: Close the window
        advWindow.Close();
    }

    /// <summary>指定された要素の子要素を再帰的にキャプチャして文字列ビルダに格納する。</summary>
    /// <param name="element">探索を開始するオートメーション要素。</param>
    /// <param name="depth">現在の探索の深さ。</param>
    /// <param name="sb">結果を格納する StringBuilder。</param>
    private void CaptureElements(AutomationElement element, int depth, System.Text.StringBuilder sb)
    {
        if (depth > 8) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                sb.AppendLine($"{indent} - {child.ControlType} Name:\"{child.Name}\", ID:\"{child.Properties.AutomationId}\", Off:\"{child.IsOffscreen}\", Rect:\"{child.BoundingRectangle}\"");
                CaptureElements(child, depth + 1, sb);
            }
        }
        catch { }
    }
}
