using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>入金フロー（開始、投入、確定、返却）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class DepositTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリを起動し、初期状態をセットアップする。</summary>
    public DepositTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    /// <summary>新規入金を開始し、現金を投入して確定するまでの一連のフローを検証する。</summary>
    [Fact]
    public void ShouldCompleteDepositFlow()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs * 2); // Wait longer for initial load

        // 1. Get initial total (Main Window)
        var totalAmountText = FindElement(window, "TotalAmountText", "\\")?.AsLabel();
        totalAmountText.ShouldNotBeNull();
        decimal initialTotal = ParseAmount(totalAmountText.Text);

        // 2. Open Terminal
        var depositWindow = OpenDepositTerminal(window);

        // 3. Begin Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 4. Fill Bulk Insert
        FillBulkInsert(depositWindow, "2");

        // 5. Verify Current Deposit Amount on Terminal Window
        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", "")?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        Retry.WhileTrue(() => ParseAmount(currentDepositText.Text ?? "0") > 0, UITestTimings.RetryLongTimeout);
        decimal deposited = ParseAmount(currentDepositText.Text ?? "0");
        deposited.ShouldBeGreaterThan(0);

        // DEBUG: Trace elements state after Bulk Insert
        Thread.Sleep(500);
        var debugFixBtn = FindElement(depositWindow, "FixDepositButton", "");
        var debugStoreBtn = FindElement(depositWindow, "StoreDepositButton", "");
        Console.WriteLine($"[DEBUG] After Bulk: FixBtn.IsEnabled={debugFixBtn?.IsEnabled}, StoreBtn.IsEnabled={debugStoreBtn?.IsEnabled}");

        // 6. Finish counting
        var fixButton = UiTestRetry.Find(() => {
            var btn = FindElement(depositWindow, "FixDepositButton", "");
            return (btn != null && btn.IsEnabled) ? btn : null;
        }, UITestTimings.RetryLongTimeout)?.AsButton();
        fixButton.ShouldNotBeNull();
        fixButton.Invoke(); // Use Invoke for reliability over SmartClick
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 7. Store
        var storeButton = UiTestRetry.Find(() => {
            var btn = FindElement(depositWindow, "StoreDepositButton", "");
            return (btn != null && btn.IsEnabled) ? btn : null;
        }, UITestTimings.RetryLongTimeout)?.AsButton();
        storeButton.ShouldNotBeNull();
        storeButton.Invoke(); // Use Invoke for reliability over SmartClick
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 8. Verify Global Total updated on Main Window
        Retry.WhileTrue(() => {
            var el = FindElement(window, "TotalAmountText", "");
            return el != null && ParseAmount(el.AsLabel().Text) == initialTotal;
        }, UITestTimings.RetryLongTimeout);
        
        var finalEl = FindElement(window, "TotalAmountText", "");
        decimal finalTotal = finalEl != null ? ParseAmount(finalEl.AsLabel().Text) : initialTotal;
        finalTotal.ShouldBe(initialTotal + deposited);
    }

    /// <summary>一括投入（Bulk Insert）機能の動作を検証する。</summary>
    [Fact]
    public void ShouldInsertBulkCash()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        
        var depositWindow = OpenDepositTerminal(window);

        // Start Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Fill Bulk
        FillBulkInsert(depositWindow, "10");

        // Verify Current Deposit Amount
        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", "")?.AsLabel();
        Retry.WhileTrue(() => ParseAmount(currentDepositText?.Text ?? "0") == 0, UITestTimings.RetryShortTimeout);
        ParseAmount(currentDepositText?.Text ?? "0").ShouldBeGreaterThan(0);
    }

    /// <summary>入金中の一時停止および再開動作、モード表示の遷移を検証する。</summary>
    [Fact]
    public void ShouldPauseAndResumeDeposit()
    {
        var window = _app.MainWindow;
        var depositWindow = OpenDepositTerminal(window);

        // 1. Start Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Trigger Counting status by inserting
        FillBulkInsert(depositWindow, "1");

        // 2. Wait for Mode Indicator to become COUNTING (Check on Main Window for global status)
        var modeIndicator = FindElement(window, "ModeIndicatorText", "")?.AsLabel();
        modeIndicator.ShouldNotBeNull();
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), UITestTimings.RetryLongTimeout);

        // 3. Click PAUSE
        var pauseButton = FindElement(depositWindow, "PauseDepositButton", "PAUSE")?.AsButton();
        pauseButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 4. Wait for Mode Indicator to become PAUSED
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("PAUSED") ?? false), UITestTimings.RetryLongTimeout);

        // 5. Click RESUME
        var resumeButton = FindElement(depositWindow, "ResumeDepositButton", "RESUME")?.AsButton();
        resumeButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 6. Wait for Mode Indicator to return to COUNTING
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), UITestTimings.RetryLongTimeout);
    }

    /// <summary>入金中に返却（RETURN）を選択した場合に、在庫が復元されることを検証する。</summary>
    [Fact]
    public void ShouldRepayDepositWhenReturning()
    {
        var window = _app.MainWindow;
        var totalAmountText = FindElement(window, "TotalAmountText", "\\")?.AsLabel();
        decimal initialTotal = ParseAmount(totalAmountText?.Text ?? "0");

        var depositWindow = OpenDepositTerminal(window);

        // Start Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Insert
        FillBulkInsert(depositWindow, "1");

        // Click "RETURN"
        var repayButton = FindElement(depositWindow, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Total should stay same
        decimal finalTotal = ParseAmount(totalAmountText?.Text ?? "0");
        finalTotal.ShouldBe(initialTotal);
    }

    /// <summary>エラー状態（ジャム等）の際にコントロールが無効化されることを検証する。</summary>
    [Fact]
    public void ShouldDisableControlsWhenErrorOccurs()
    {
        var window = _app.MainWindow;
        var depositWindow = OpenDepositTerminal(window);

        // Verify initial state
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "")?.AsButton();
        var quickDepositBox = FindElement(depositWindow, "QuickDepositBox", "")?.AsTextBox();
        var quickDepositButton = FindElement(depositWindow, "QuickDepositButton", "")?.AsButton();
        
        beginButton.ShouldNotBeNull();
        quickDepositBox.ShouldNotBeNull();
        quickDepositButton.ShouldNotBeNull();

        // Enter amount so QuickDepositButton evaluates CanExecute to true
        quickDepositBox.Text = "1000";
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        beginButton.IsEnabled.ShouldBeTrue();
        quickDepositBox.IsEnabled.ShouldBeTrue();
        quickDepositButton.IsEnabled.ShouldBeTrue();

        // Simulate Jam
        var simulateJamButton = FindElement(depositWindow, "SimulateJamButton", "")?.AsButton();
        simulateJamButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Verify disabled state
        beginButton.IsEnabled.ShouldBeFalse();
        quickDepositBox.IsEnabled.ShouldBeFalse();
        quickDepositButton.IsEnabled.ShouldBeFalse();

        // Reset Error
        var resetErrorButton = FindElement(depositWindow, "ResetErrorButton", "")?.AsButton();
        resetErrorButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Verify enabled state
        beginButton.IsEnabled.ShouldBeTrue();
        quickDepositBox.IsEnabled.ShouldBeTrue();
        quickDepositButton.IsEnabled.ShouldBeTrue();
    }

    /// <summary>紙幣重なりエラーが発生した際に、入金確定がブロックされることを検証する。</summary>
    [Fact]
    public void ShouldPreventFixWhenOverlapped()
    {
        var window = _app.MainWindow;
        var depositWindow = OpenDepositTerminal(window);

        // 1. Begin Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 2. Click "ERROR" (Simulate Overlap)
        var overlapButton = UiTestRetry.Find(() => FindElement(depositWindow, "SimulateOverlapButton", "")?.AsButton(), UITestTimings.RetryLongTimeout);
        overlapButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 3. Verify Overlap indicator appears
        var errorIndicator = UiTestRetry.Find(() => window!.FindFirstDescendant(cf => cf.ByAutomationId("OverlapErrorIndicator")), UITestTimings.RetryLongTimeout);
        errorIndicator.ShouldNotBeNull();

        // 4. Try to click FINISH (FixDeposit) -> Should be disabled
        var fixButton = FindElement(depositWindow, "FixDepositButton", "")?.AsButton();
        fixButton.ShouldNotBeNull();
        Retry.WhileTrue(() => fixButton.IsEnabled, UITestTimings.RetryLongTimeout);
        fixButton.IsEnabled.ShouldBeFalse();

        // Verify Overlap indicator still exists
        window!.FindFirstDescendant(cf => cf.ByAutomationId("OverlapErrorIndicator")).ShouldNotBeNull();

        // 5. Cancel (RETURN) should work, but does NOT clear the hardware error
        var repayButton = FindElement(depositWindow, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 6. Manually reset the hardware error
        var resetErrorButton = FindElement(depositWindow, "ActiveResetErrorButton", "")?.AsButton();
        resetErrorButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Error indicator should be gone
        window.FindFirstDescendant(cf => cf.ByAutomationId("OverlapErrorIndicator")).ShouldBeNull();
    }

    private Window OpenDepositTerminal(Window? mainWindow)
    {
        var launchButton = UiTestRetry.Find(() => mainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDepositButton"))?.AsButton(), UITestTimings.RetryLongTimeout);
        launchButton.SmartClick();

        Thread.Sleep(UITestTimings.WindowPopupDelayMs);
        var depositWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DepositWindow", UITestTimings.RetryLongTimeout);
        depositWindow.ShouldNotBeNull();
        depositWindow.SetForeground();
        return depositWindow;
    }

    private void FillBulkInsert(Window depositWindow, string quantity)
    {
        var bulkButton = FindElement(depositWindow, "BulkInsertButton", "BULK")?.AsButton();
        bulkButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        // Find the new dialog window
        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", UITestTimings.RetryLongTimeout);
        dialog.ShouldNotBeNull();

        var firstTextBox = UiTestRetry.Find(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("BulkQuantityBox"))?.AsTextBox(), UITestTimings.RetryLongTimeout) as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Focus();
        Thread.Sleep(100);
        FlaUI.Core.Input.Keyboard.Type(quantity);
        Thread.Sleep(100);
        FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN);
        Thread.Sleep(500); // Give WPF binding a moment to catch up

        var executeButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        executeButton?.Focus(); // Force focus loss on TextBox to trigger PropertyChanged commit
        Thread.Sleep(100);
        executeButton.SmartClick(timeoutMs: 1000); // Shorter timeout for conditional
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);
    }

    private static AutomationElement? FindElement(AutomationElement? container, string? automationId, string? text)
    {
        return container == null
            ? null
            : UiTestRetry.Find(() =>
        {
            // Try by AutomationId first (highest priority) if provided
            if (!string.IsNullOrEmpty(automationId))
            {
                var el = container.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (el != null) return el;
            }

            // Fallback to text search if provided
            if (!string.IsNullOrEmpty(text))
            {
                var elByText = container.FindFirstDescendant(cf => cf.ByText(text));
                if (elByText != null) return elByText;
            }
            return null;
        }, UITestTimings.RetryLongTimeout);
    }

    private static decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var cleaned = new string([.. text.Where(char.IsDigit)]);
        return decimal.TryParse(cleaned, out var result) ? result : 0;
    }

    public void Dispose()
    {
        _app?.Dispose();
        GC.SuppressFinalize(this);
    }
}
