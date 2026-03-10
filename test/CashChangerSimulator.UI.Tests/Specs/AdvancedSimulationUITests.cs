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
        var retry = 20;
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
                var found = window.FindFirstDescendant(cf => cf.ByAutomationId("AdvancedSimulationWindow")
                    .Or(cf.ByName("高度なシミュレーション操作")));
                if (found != null) advWindow = found.AsWindow();
            }

            if (advWindow != null) break;

            var names = string.Join(", ", appWindows.Select(w => $"'{w.Name}' ({w.AutomationId})"));
            Thread.Sleep(1000);
            retry--;
            if (retry == 0) throw new Exception($@"Advanced Simulation window not found. Found app windows: {names}. Main window descendants: {tree}");
        }

        advWindow.ShouldNotBeNull("Advanced Simulation window should be open.");

        var resetBtn = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("ResetErrorButton")), TimeSpan.FromSeconds(5)).Result?.AsButton();
        var simulateJamBtn = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton")), TimeSpan.FromSeconds(5)).Result?.AsButton();

        resetBtn.ShouldNotBeNull("ResetErrorButton not found");
        simulateJamBtn.ShouldNotBeNull("SimulateJamButton not found");

        // Act: Simulate Jam
        if (simulateJamBtn.Patterns.Invoke.IsSupported) simulateJamBtn.Patterns.Invoke.Pattern.Invoke();
        else simulateJamBtn.Click();

        // Assert: Now it should be in the tree and visible
        var jamIndicator = Retry.WhileNull(() => advWindow.FindFirstDescendant(cf => cf.ByAutomationId("JamIndicatorText")), TimeSpan.FromSeconds(10)).Result;
        jamIndicator.ShouldNotBeNull("JamIndicator should be visible after simulation");
        jamIndicator.IsOffscreen.ShouldBeFalse();

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
