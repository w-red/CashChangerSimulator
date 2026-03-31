using System.Runtime.InteropServices;
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
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Device did not enter IDLE state in GlobalReset test");

        // 念のため画面が表示されるのを待つ
        var sidebarJamBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("HeaderSimulateJamButton")), TimeSpan.FromSeconds(10)).Result?.AsToggleButton();
        sidebarJamBtn.ShouldNotBeNull("Header SimulateJamButton not found");

        // Act: Header の Jam ボタンを Invoke してエラー状態にする
        var jamBtnElement = window.FindFirstDescendant(cf => cf.ByAutomationId("HeaderSimulateJamButton"))?.AsButton();
        jamBtnElement.ShouldNotBeNull("HeaderSimulateJamButton found for jamming");
        jamBtnElement!.Patterns.Invoke.Pattern.Invoke();

        // [STABILITY] 状態がプロパティおよびリセットボタンに反映されるのを待機
        Retry.WhileFalse(() => {
            try {
                var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
                Thread.Sleep(500); // Be gentle
                return indicator != null && indicator.IsEnabled;
            } catch (COMException) { return false; }
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Jam state was not detected by JamErrorIndicator_State");

        // GlobalResetErrorButton はエラー発生時のみ出現する (Visibility Trigger)
        // 階層が深い可能性があるため、FindFirstDescendant ではなく広範囲を探索。
        // また、Window が複数ある可能性（DialogHost等）を考慮し、全要素から探索。
        var resetBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("GlobalResetErrorButton")), TimeSpan.FromSeconds(30)).Result?.AsButton();
        
        if (resetBtn == null)
        {
            // デバッグ情報収集
            var allButtons = window.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button));
            var buttonIds = string.Join(", ", allButtons.Select(b => b.Properties.AutomationId.ValueOrDefault ?? "NoID"));
            throw new Exception($"GlobalResetErrorButton not found. Visible Buttons: [{buttonIds}]");
        }
        
        Retry.WhileFalse(() => resetBtn.IsEnabled, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("GlobalResetErrorButton should be enabled when jammed");

        // Assert: Jam インジケータを確認
        var jamIndicator = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State")), TimeSpan.FromSeconds(10)).Result;
        jamIndicator.ShouldNotBeNull("JamErrorIndicator_State should be present in status header");
        Retry.WhileFalse(() => jamIndicator.IsEnabled, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("JamErrorIndicator_State should be enabled when jammed");

        // Act: Reset ボタンをクリック
        resetBtn.ShouldNotBeNull();
        resetBtn!.Patterns.Invoke.Pattern.Invoke();

        // 状態が自動的に Off になるのを待機する（手動で ToggleButton を操作すると逆に再ジャムする可能性があるため）
        // 状態が自動的に Off になるのを待機する（ヘッダー等は ViewModel から反映される）
        Retry.WhileFalse(() => {
            var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
            return indicator == null || !indicator.IsEnabled;
        }, TimeSpan.FromSeconds(15)).Success.ShouldBeTrue("JamErrorIndicator_State should be disabled after reset");

        // Assert: エラー状態が解消されたことを確認
        Retry.WhileFalse(() => {
            var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
            return indicator == null || !indicator.IsEnabled;
        }, TimeSpan.FromSeconds(15))
             .Success.ShouldBeTrue("JamErrorIndicator_State should be disabled after reset");
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

        // Act: Header の Jam ボタンを Invoke してエラー状態にする
        var jamBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("HeaderSimulateJamButton")), TimeSpan.FromSeconds(15)).Result?.AsButton();
        jamBtn.ShouldNotBeNull("HeaderSimulateJamButton not found for GlobalReset test");
        jamBtn!.Patterns.Invoke.Pattern.Invoke();

        // [STABILITY] 状態が反映されるのを待機
        Retry.WhileFalse(() => {
            try {
                var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
                Thread.Sleep(500);
                return indicator != null && indicator.IsEnabled;
            } catch (COMException) { return false; }
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Jam state was not detected by JamErrorIndicator_State during GlobalReset test");

        // Global Reset ボタン（ヘッダー部分）はエラー発生時にのみ Visible になり UI ツリーに出現する
        var globalResetBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("GlobalResetErrorButton")), TimeSpan.FromSeconds(30)).Result?.AsButton();
        globalResetBtn.ShouldNotBeNull("GlobalResetErrorButton not found");
        Retry.WhileFalse(() => globalResetBtn.IsEnabled, TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("GlobalResetErrorButton should be enabled when jammed");

        // Act: Click Global Reset (Using Invoke for stability)
        globalResetBtn.ShouldNotBeNull();
        globalResetBtn!.Patterns.Invoke.Pattern.Invoke();

        // 状態が自動的に Off になるのを待機する
        // 状態が自動的に Off になるのを待機する
        Retry.WhileFalse(() => {
            var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
            return indicator == null || !indicator.IsEnabled;
        }, TimeSpan.FromSeconds(15)).Success.ShouldBeTrue("Jam state should be cleared after global reset");

        // Assert
        Retry.WhileFalse(() => {
            var indicator = window.FindFirstDescendant(cf => cf.ByAutomationId("JamErrorIndicator_State"));
            return indicator == null || !indicator.IsEnabled;
        }, TimeSpan.FromSeconds(15))
             .Success.ShouldBeTrue("JamErrorIndicator_State should be disabled after global reset");
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
        
        launchDispenseBtn.ShouldNotBeNull();
        if (launchDispenseBtn!.Patterns.Invoke.IsSupported) launchDispenseBtn.Patterns.Invoke.Pattern.Invoke();
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
        var dispenseJamBtn = dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("DispenseSimulateJamButton"))?.AsButton();
        dispenseJamBtn.ShouldNotBeNull("DispenseSimulateJamButton not found in dispense window");
        
        if (dispenseJamBtn!.Patterns.Invoke.IsSupported) dispenseJamBtn.Patterns.Invoke.Pattern.Invoke();
        else dispenseJamBtn.SmartClick();

        // [STABILITY] Wait for error visual state to be fully loaded
        Retry.WhileNull(() => dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("DispenseErrorResetButton")), TimeSpan.FromSeconds(20))
            .Success.ShouldBeTrue("DispenseErrorResetButton did not appear");

        // エラー解消ボタンを探す (オーバーレイまたは通常ビュー内のリセットボタン)
        var overlayResetBtn = Retry.WhileNull(() => 
            dispenseWindow.FindFirstDescendant(cf => cf.ByAutomationId("DispenseErrorResetButton")), 
            TimeSpan.FromSeconds(15)).Result?.AsButton();
        overlayResetBtn.ShouldNotBeNull("Dispense reset button not found (DispenseErrorResetButton)");

        // Act: Overlay の Reset をクリック
        overlayResetBtn.SmartClick();

        // Assert: エラー表示が消えたことを確認（オーバーレイのボタンがDisabledになる）
        Retry.WhileFalse(() => overlayResetBtn != null && !overlayResetBtn.IsEnabled, TimeSpan.FromSeconds(5))
             .Success.ShouldBeTrue("DispenseErrorResetButton should be disabled after reset");

        dispenseWindow.Close();
    }
}
