using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>入金フロー（開始、投入、確定、返却）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class DepositTest : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリのフィクスチャを受け取り、初期状態をセットアップする。</summary>
    public DepositTest(CashChangerTestApp app)
    {
        _app = app;
    }

    /// <summary>一連の入金フロー（開始、投入、確定、収納）を検証する。</summary>
    [Fact]
    public void ShouldCompleteDepositFlow()
    {
        _app.Launch();
        var window = _app.MainWindow;
        if (window == null) throw new Exception("MainWindow is null");
        window.SetForeground();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs * 2);

        var totalAmountText = FindElement(window, "TotalAmountText", null)?.AsLabel();
        totalAmountText.ShouldNotBeNull();
        var initialTotal = ParseAmount(totalAmountText.Text);

        var depositWindow = OpenDepositTerminal(window);
        depositWindow.ShouldNotBeNull();

        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 一括投入 (5250円を再現するため、1円玉のボックス(Index 9)に5250を入力)
        FillBulkInsert(depositWindow, "5250", 9);

        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", null)?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        UiTestRetry.Find(() => ParseAmount(currentDepositText.Text) == 5250 ? currentDepositText : null, TimeSpan.FromSeconds(5)).ShouldNotBeNull();

        var fixButton = FindElement(depositWindow, "FixDepositButton", "FINISH")?.AsButton();
        fixButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        var storeButton = FindElement(depositWindow, "StoreDepositButton", "STORE")?.AsButton();
        storeButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        var newTotalText = FindElement(window, "TotalAmountText", null)?.AsLabel();
        newTotalText.ShouldNotBeNull();
        ParseAmount(newTotalText.Text).ShouldBe(initialTotal + 5250);
    }

    /// <summary>一括投入ダイアログを使用して現金を投入できることを検証する。</summary>
    [Fact]
    public void ShouldInsertBulkCash()
    {
        _app.Launch();
        var window = _app.MainWindow;
        if (window == null) throw new Exception("MainWindow is null");
        window.SetForeground();

        var depositWindow = OpenDepositTerminal(window);
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START")?.AsButton();
        beginButton.SmartClick();

        var bulkButton = FindElement(depositWindow, "BulkInsertButton", "BULK")?.AsButton();
        bulkButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", timeout: UITestTimings.RetryLongTimeout);
        dialog.ShouldNotBeNull();

        var firstTextBox = UiTestRetry.Find(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("BulkQuantityBox"))?.AsTextBox(), UITestTimings.RetryLongTimeout) as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "3";
        Thread.Sleep(100);
        firstTextBox.Focus();
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(500);

        var okButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        okButton.SmartClick();

        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", null)?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        
        // Wait for update
        Retry.WhileFalse(() => ParseAmount(currentDepositText.Text) > 0, UITestTimings.RetryLongTimeout).Success
            .ShouldBeTrue($"Current deposit should be greater than 0. Actual: {currentDepositText.Text}");
    }

    /// <summary>入金をキャンセルして投入済みの現金を返却できることを検証する。</summary>
    [Fact]
    public void ShouldCancelDepositAndRepay()
    {
        _app.Launch();
        var window = _app.MainWindow;
        if (window == null) throw new Exception("MainWindow is null");
        
        var depositWindow = OpenDepositTerminal(window);
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START")?.AsButton();
        beginButton.SmartClick();

        FillBulkInsert(depositWindow, "1000");

        var repayButton = FindElement(depositWindow, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        // Window should close
        Retry.WhileTrue(() => _app.Application.GetAllTopLevelWindows(_app.Automation).Any(w => w.AutomationId == "DepositWindow"), TimeSpan.FromSeconds(5)).Success.ShouldBeTrue("Window should be closed");
    }

    /// <summary>エラー（ジャム）発生時にコントロールが適切に無効化されることを検証する。</summary>
    [Fact]
    public void ShouldDisableControlsWhenErrorOccurs()
    {
        _app.Launch();
        var window = _app.MainWindow;
        if (window == null) throw new Exception("MainWindow is null");
        
        var depositWindow = OpenDepositTerminal(window);
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START")?.AsButton();
        beginButton.SmartClick();

        // Wait for template to switch and UI to settle
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);
        
        var jamButton = UiTestRetry.Find(() => depositWindow.FindFirstDescendant(cf => cf.ByAutomationId("SimulateJamButton"))?.AsButton(), TimeSpan.FromSeconds(20));
        
        if (jamButton == null)
        {
            UiTestRetry.DumpAutomationTree(depositWindow, "ShouldDisableControls_JamButtonMissing");
            throw new Exception("SimulateJamButton not found in DepositWindow. Check AutomationTree dump.");
        }
        jamButton.SmartClick();

        var bulkButton = FindElement(depositWindow, "BulkInsertButton", "BULK", TimeSpan.FromSeconds(20))?.AsButton();
        if (bulkButton == null)
        {
            UiTestRetry.DumpAutomationTree(depositWindow, "ShouldDisableControls_BulkButtonMissing");
            bulkButton.ShouldNotBeNull("BulkInsertButton not found");
        }
        Retry.WhileTrue(() => bulkButton.IsEnabled, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("Bulk button should be disabled during error.");

        var fixButton = FindElement(depositWindow, "FixDepositButton", "FINISH")?.AsButton();
        Retry.WhileTrue(() => fixButton != null && fixButton.IsEnabled, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("Fix button should be disabled during error.");

        var resetButton = FindElement(depositWindow, "ActiveResetErrorButton", "RESET")?.AsButton();
        resetButton.ShouldNotBeNull();
        resetButton.IsEnabled.ShouldBeTrue();
        resetButton.SmartClick();
        Thread.Sleep(500);

        bulkButton.IsEnabled.ShouldBeTrue();
    }

    /// <summary>メインウィンドウから入金ウィンドウを探して開く。</summary>
    /// <param name="mainWindow">メインウィンドウのオートメーション要素。</param>
    /// <returns>開かれた入金ウィンドウ。</returns>
    private Window OpenDepositTerminal(Window mainWindow)
    {
        var launchButton = UiTestRetry.Find(() => mainWindow.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDepositButton"))?.AsButton(), TimeSpan.FromSeconds(15)) as Button;
        launchButton.ShouldNotBeNull();
        
        launchButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        var depositWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DepositWindow", timeout: TimeSpan.FromSeconds(30), markerId: "DepositWindowMark");
        
        if (depositWindow == null)
        {
            UiTestRetry.DumpAutomationTree(mainWindow, "OpenDepositTerminal_Fail");
            throw new Exception("DepositWindow not found or not fully initialized (Marker missing).");
        }

        depositWindow.SetForeground();
        return depositWindow;
    }

    /// <summary>一括投入ダイアログを開いて指定された数量を入力する。</summary>
    /// <param name="depositWindow">入金ウィンドウ。</param>
    /// <param name="quantity">入力する数量文字列。</param>
    /// <param name="denomIndex">入力対象の金種インデックス。</param>
    private void FillBulkInsert(Window depositWindow, string quantity, int denomIndex = 0)
    {
        var bulkButton = FindElement(depositWindow, "BulkInsertButton", "BULK")?.AsButton();
        bulkButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", timeout: UITestTimings.RetryLongTimeout);
        dialog.ShouldNotBeNull();

        var textBoxes = UiTestRetry.Find(() => 
        {
            var all = dialog.FindAllDescendants(cf => cf.ByAutomationId("BulkQuantityBox"));
            return all.Length > denomIndex ? all : null;
        }, UITestTimings.RetryLongTimeout);
        textBoxes.ShouldNotBeNull();
        
        var targetTextBox = textBoxes[denomIndex].AsTextBox();
        targetTextBox.Text = quantity;
        Thread.Sleep(500);
        targetTextBox.Focus();
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(500);

        var executeButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        executeButton?.Focus();
        Thread.Sleep(100);
        executeButton.SmartClick(timeoutMs: 1000);
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);
    }

    /// <summary>オートメーションIDまたは表示名を使用して要素を探索する。</summary>
    /// <param name="parent">親要素。</param>
    /// <param name="automationId">探索するオートメーションID。</param>
    /// <param name="fallbackName">IDで見つからない場合の代替表示名。</param>
    /// <param name="timeout">タイムアウト時間。</param>
    /// <returns>見つかった要素、または null。</returns>
    private AutomationElement? FindElement(AutomationElement parent, string automationId, string? fallbackName = null, TimeSpan? timeout = null)
    {
        var finalTimeout = timeout ?? TimeSpan.FromSeconds(10);
        return UiTestRetry.Find(() => 
        {
            // First try direct descendant
            var el = parent.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el != null) return el;
            
            // Then try all descendants (slower but more robust)
            el = parent.FindAllDescendants().FirstOrDefault(e => e.Properties.AutomationId.ValueOrDefault == automationId);
            if (el != null) return el;
            
            // Finally try fallback name
            if (fallbackName != null)
            {
                el = parent.FindFirstDescendant(cf => cf.ByName(fallbackName));
                if (el != null) return el;
            }
            
            return null;
        }, finalTimeout);
    }

    /// <summary>数値以外の文字を含む文字列から金額をパースする。</summary>
    /// <param name="text">パース対象の文字列。</param>
    /// <returns>パースされた金額。</returns>
    private static decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var cleaned = new string([.. text.Where(char.IsDigit)]);
        return decimal.TryParse(cleaned, out var result) ? result : 0;
    }
}
