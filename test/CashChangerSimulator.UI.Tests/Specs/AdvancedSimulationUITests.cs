using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

public class AdvancedSimulationUITests : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    public AdvancedSimulationUITests(CashChangerTestApp app)
    {
        _app = app;
    }

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
            openBtn.AsButton().Click();
        }

        // 接続完了まで待機
        var closeBtn = Retry.WhileNull(() => window.FindFirstDescendant("DeviceCloseButton"), TimeSpan.FromSeconds(15)).Result;
        closeBtn.ShouldNotBeNull($@"DeviceCloseButton not found. Tree: {tree}");
        closeBtn.WaitUntilEnabled(TimeSpan.FromSeconds(15));

        // Advanced Simulation ウィンドウを開く
        var launchBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchAdvancedSimulationButton"));
        if (launchBtn == null) throw new Exception($@"LaunchAdvancedSimulationButton not found. Tree: {tree}");
        
        // 有効になるまで待つ
        Retry.WhileFalse(() => launchBtn.IsEnabled, TimeSpan.FromSeconds(10));
        
        // フォーカスを当ててからクリック
        launchBtn.Focus();
        if (launchBtn.Patterns.Invoke.IsSupported)
        {
            launchBtn.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            launchBtn.AsButton().Click();
        }
        
        // click後のウィンドウリスト取得用（自プロセスのみ）
        var desktop = _app.Automation.GetDesktop();
        
        // ウィンドウを検索 (堅牢な検索)
        Window? advWindow = null;
        var retry = 30; // Longer retry for CI
        while (retry > 0)
        {
            var appWindows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                .Where(w => w.Properties.ProcessId.ValueOrDefault == _app.Application.ProcessId)
                .ToList();
            
            advWindow = appWindows.FirstOrDefault(w => 
            {
                try { return w.AutomationId == "AdvancedSimulationWindow" || (w.Name != null && (w.Name.Contains("Simulation") || w.Name.Contains("シミュレーション"))); } catch { return false; }
            })?.AsWindow();
            
            if (advWindow == null)
            {
                // Try finding within MainWindow in case it's treated as a child
                var found = window.FindFirstDescendant(cf => cf.ByAutomationId("AdvancedSimulationWindow")
                    .Or(cf.ByName("高度なシミュレーション操作").Or(cf.ByName("Advanced Simulation"))));
                if (found != null) advWindow = found.AsWindow();
            }

            if (advWindow != null) break;

            Thread.Sleep(1000);
            retry--;
        }

        if (advWindow != null)
        {
            try
            {
                advWindow.SetForeground();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INFO] SetForeground failed in CI: {ex.Message}");
            }
        }
        advWindow.ShouldNotBeNull("Advanced Simulation window should be open.");

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

        var simulateJamBtn = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton")), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(2)).Result?.AsButton();

        simulateJamBtn.ShouldNotBeNull("SimulateJamButton not found in AdvancedSimulationWindow");

        // Act: Simulate Jam
        if (simulateJamBtn.Patterns.Invoke.IsSupported) simulateJamBtn.Patterns.Invoke.Pattern.Invoke();
        else simulateJamBtn.Click();

        // Assert: Now it should be in the tree and visible
        var jamIndicator = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("JamIndicatorText")), TimeSpan.FromSeconds(10)).Result;
        jamIndicator.ShouldNotBeNull("JamIndicator should be visible after simulation");
        jamIndicator.IsOffscreen.ShouldBeFalse();

        // ResetErrorButton は常にツリーに配置されているが、エラー発生後にのみ Enabled となる
        var resetBtn = advWindow.FindFirstDescendant(cf => cf.ByAutomationId("ResetErrorButton"))?.AsButton();
        resetBtn.ShouldNotBeNull("ResetErrorButton not found");
        
        Retry.WhileFalse(() => resetBtn.IsEnabled, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("ResetErrorButton should enable after jam");

        // Act: Reset
        if (resetBtn.Patterns.Invoke.IsSupported) resetBtn.Patterns.Invoke.Pattern.Invoke();
        else resetBtn.Click();

        // Assert: Jam indicator should disappear (Visibility=Collapsed)
        Retry.WhileFalse(() => {
            var indicator = advWindow.FindFirstDescendant(cf => cf.ByAutomationId("JamIndicatorText"));
            return indicator == null || indicator.IsOffscreen;
        }, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("JamIndicator should be hidden after reset");

        // Cleanup: Close the window
        advWindow.Close();
    }
}
