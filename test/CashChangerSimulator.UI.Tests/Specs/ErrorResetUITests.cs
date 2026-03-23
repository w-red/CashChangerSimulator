using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.Core.Definitions;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>エラー状態の解消（リセット）機能が、各 UI 要素から正しく動作することを検証する FlaUI テスト。</summary>
[Collection("SequentialTests")]
public class ErrorResetUITests : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    public ErrorResetUITests(CashChangerTestApp app)
    {
        _app = app;
    }

    /// <summary>サイドバーのリセットボタンによりエラー状態が解消されることを検証する。</summary>
    [Fact]
    public void SidebarResetButtonShouldClearErrorState()
    {
        // Arrange
        _app.Launch(hotStart: true);
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");

        // デバイスが接続されていない場合は接続する (Open)
        var openBtn = window.FindFirstDescendant("DeviceOpenButton");
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            openBtn.AsButton().SmartClick();
        }

        // ステータスが IDLE になるまで待機 (HotStart時も含む)
        Retry.WhileFalse(() => {
            var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel();
            return modeText != null && (modeText.Text == "IDLE" || modeText.Text == "待機中");
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Device did not enter IDLE state");

        // 念のため画面が表示されるのを待つ
        var sidebarJamBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton")), TimeSpan.FromSeconds(10)).Result?.AsButton();
        sidebarJamBtn.ShouldNotBeNull("Sidebar SimulateJamButton not found");

        // Act: Sidebar の Jam ボタン（またはヘッダーのシミュレーションボタン）を押してエラー状態にする
        sidebarJamBtn.SmartClick();

        // ヘッダーの Reset ボタンはエラー発生時にのみ Visible になり UI ツリーに出現する
        // 直系に見つからない場合があるため、StatusHeaderControl の中を明示的に探す
        var statusHeader = window.FindFirstDescendant(cf => cf.ByAutomationId("GlobalStatusHeader"));
        var resetBtn = Retry.WhileNull(() => statusHeader?.FindFirstDescendant(cf => cf.ByAutomationId("GlobalResetErrorButton")), TimeSpan.FromSeconds(20)).Result?.AsButton();
        resetBtn.ShouldNotBeNull("GlobalResetErrorButton not found within GlobalStatusHeader");
        Retry.WhileFalse(() => resetBtn != null && resetBtn.IsEnabled, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("GlobalResetErrorButton should be enabled when jammed");

        // Assert: Jam インジケータ（ステータスタブなど）を確認
        var jamIndicator = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator")), TimeSpan.FromSeconds(10)).Result;
        jamIndicator.ShouldNotBeNull("JamErrorIndicator should be visible in status header");

        // Act: Reset ボタンをクリック
        resetBtn.SmartClick();

        // Assert: エラー状態が解消されたことを確認
        Retry.WhileFalse(() => window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator")) == null, TimeSpan.FromSeconds(5))
             .Success.ShouldBeTrue("JamErrorIndicator should disappear after reset");
    }

    /// <summary>グローバルリセットボタンによりエラー状態が解消されることを検証する。</summary>
    [Fact]
    public void GlobalResetButtonShouldClearErrorState()
    {
        // Arrange
        _app.Launch(hotStart: true);
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");

        // デバイスが接続されていない場合は接続する (Open)
        var openBtn = window.FindFirstDescendant("DeviceOpenButton");
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            openBtn.AsButton().SmartClick();
        }

        // ステータスが IDLE になるまで待機
        Retry.WhileFalse(() => {
            var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel();
            return modeText != null && (modeText.Text == "IDLE" || modeText.Text == "待機中");
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Device did not enter IDLE state");

        var sidebarJamBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton")), TimeSpan.FromSeconds(10)).Result?.AsButton();
        sidebarJamBtn.ShouldNotBeNull();

        // Act: Simulate Jam
        sidebarJamBtn.SmartClick();

        // Global Reset ボタン（ヘッダー部分）はエラー発生時にのみ Visible になり UI ツリーに出現する
        // 直系に見つからない場合があるため、StatusHeaderControl の中を明示的に探す
        var statusHeader = window.FindFirstDescendant(cf => cf.ByAutomationId("GlobalStatusHeader"));
        var globalResetBtn = Retry.WhileNull(() => statusHeader?.FindFirstDescendant(cf => cf.ByAutomationId("GlobalResetErrorButton")), TimeSpan.FromSeconds(20)).Result?.AsButton();
        globalResetBtn.ShouldNotBeNull("GlobalResetErrorButton not found within GlobalStatusHeader");
        Retry.WhileFalse(() => globalResetBtn != null && globalResetBtn.IsEnabled, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("GlobalResetErrorButton should be enabled when jammed");

        // Act: Click Global Reset
        globalResetBtn.SmartClick();

        // Assert
        Retry.WhileFalse(() => window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator")) == null, TimeSpan.FromSeconds(5))
             .Success.ShouldBeTrue("JamErrorIndicator should disappear after global reset");
    }

    /// <summary>出金エラー時のオーバーレイリセットボタンによりエラー状態が解消されることを検証する。</summary>
    [Fact]
    public void DispenseErrorOverlayResetButtonShouldClearErrorState()
    {
        // Arrange: 金額 1 で出金遅延を設定してエラーを誘発するか、
        // または DispenseWindow を開き、オーバーレイが表示される状態でテストする。
        _app.Launch(hotStart: true);
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");

        // デバイスが接続されていない場合は接続する (Open)
        var openBtn = window.FindFirstDescendant("DeviceOpenButton");
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            openBtn.AsButton().SmartClick();
        }

        // ステータスが IDLE になるまで待機
        Retry.WhileFalse(() => {
            var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel();
            return modeText != null && (modeText.Text == "IDLE" || modeText.Text == "待機中");
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Device did not enter IDLE state");

        // 出金ウィンドウを開く
        var launchDispenseBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDispenseButton"))?.AsButton();
        launchDispenseBtn.ShouldNotBeNull("LaunchDispenseButton not found");
        
        // 有効になるまで待機
        Retry.WhileFalse(() => launchDispenseBtn != null && launchDispenseBtn.IsEnabled, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("LaunchDispenseButton was not enabled");
        
        if (launchDispenseBtn.Patterns.Invoke.IsSupported) launchDispenseBtn.Patterns.Invoke.Pattern.Invoke();
        else launchDispenseBtn.SmartClick();

        // 出金ウィンドウを待機 (デスクトップおよびメインウィンドウの直系から検索)
        var desktop = _app.Automation.GetDesktop();
        var dispenseWindow = Retry.WhileNull(() => 
        {
            // デスクトップから検索
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            var found = windows.FirstOrDefault(w => 
            {
                var aid = w.Properties.AutomationId.ValueOrDefault;
                var name = w.Properties.Name.ValueOrDefault;
                return aid == "DispenseWindow" || (name != null && name.Contains("DISPENSE"));
            });
            if (found != null) return found.AsWindow();

            // メインウィンドウの子から検索 (念のため)
            found = window.FindAllChildren(cf => cf.ByControlType(ControlType.Window)).FirstOrDefault(w => 
            {
                var aid = w.Properties.AutomationId.ValueOrDefault;
                var name = w.Properties.Name.ValueOrDefault;
                return aid == "DispenseWindow" || (name != null && name.Contains("DISPENSE"));
            });
            return found?.AsWindow();
        }, TimeSpan.FromSeconds(15)).Result;

        if (dispenseWindow == null)
        {
            var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            var names = string.Join(", ", allWindows.Select(w => 
            {
                var n = w.Properties.Name.ValueOrDefault ?? "Unknown";
                var aid = w.Properties.AutomationId.ValueOrDefault ?? "Unknown";
                var pid = w.Properties.ProcessId.ValueOrDefault;
                return $"'{n}' ({aid}) [PID={pid}]";
            }));
            throw new Exception($"DispenseWindow not found. Found windows: {names}");
        }

        // Act: 出金ウィンドウ内の Jam シミュレーションボタンをクリック
        var sidebarJamBtn = dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton"))?.AsButton();
        sidebarJamBtn.ShouldNotBeNull("SimulateJamButton not found in dispense window");
        
        if (sidebarJamBtn.Patterns.Invoke.IsSupported) sidebarJamBtn.Patterns.Invoke.Pattern.Invoke();
        else sidebarJamBtn.SmartClick();

        // エラー解消ボタンを探す (オーバーレイまたは通常ビュー内のリセットボタン)
        var overlayResetBtn = Retry.WhileNull(() => 
            dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("DispenseErrorResetButton")) ??
            dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("ResetErrorButton")), 
            TimeSpan.FromSeconds(15)).Result?.AsButton();
        overlayResetBtn.ShouldNotBeNull("Dispense reset button not found (DispenseErrorResetButton or ResetErrorButton)");

        // Act: Overlay の Reset をクリック
        overlayResetBtn.SmartClick();

        // Assert: エラー表示が消えたことを確認（オーバーレイのボタンがDisabledになる）
        Retry.WhileFalse(() => overlayResetBtn != null && !overlayResetBtn.IsEnabled, TimeSpan.FromSeconds(5))
             .Success.ShouldBeTrue("DispenseErrorResetButton should be disabled after reset");

        dispenseWindow.Close();
    }
}
