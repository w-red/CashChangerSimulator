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
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 1. Get initial total (Main Window)
        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        totalAmountText.ShouldNotBeNull();
        decimal initialTotal = ParseAmount(totalAmountText.Text);

        // 2. Open Terminal
        var depositWindow = OpenDepositTerminal(window);

        // 3. Begin Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 4. Fill Bulk Insert
        FillBulkInsert(depositWindow, "2");

        // 5. Verify Current Deposit Amount on Terminal Window
        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", null)?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        Retry.WhileTrue(() => ParseAmount(currentDepositText.Text ?? "0") == 0, UITestTimings.RetryLongTimeout);
        decimal deposited = ParseAmount(currentDepositText.Text ?? "0");
        deposited.ShouldBeGreaterThan(0);

        // 6. Finish counting
        var fixButton = FindElement(depositWindow, "FixDepositButton", "FINISH COUNTING")?.AsButton();
        fixButton.ShouldNotBeNull();
        fixButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 7. Store
        var storeButton = FindElement(depositWindow, "StoreDepositButton", "STORE")?.AsButton();
        storeButton.ShouldNotBeNull();
        storeButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 8. Verify Global Total updated on Main Window
        Retry.WhileTrue(() => ParseAmount(totalAmountText.Text) == initialTotal, UITestTimings.RetryLongTimeout);
        decimal finalTotal = ParseAmount(totalAmountText.Text);
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
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Fill Bulk
        FillBulkInsert(depositWindow, "10");

        // Verify Current Deposit Amount
        var currentDepositText = FindElement(depositWindow, "CurrentDepositText", null)?.AsLabel();
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
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Trigger Counting status by inserting
        FillBulkInsert(depositWindow, "1");

        // 2. Wait for Mode Indicator to become COUNTING (Check on Main Window for global status)
        var modeIndicator = FindElement(window, "ModeIndicatorText", null)?.AsLabel();
        modeIndicator.ShouldNotBeNull();
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), UITestTimings.RetryLongTimeout);

        // 3. Click PAUSE
        var pauseButton = FindElement(depositWindow, "PauseDepositButton", "PAUSE")?.AsButton();
        pauseButton.ShouldNotBeNull();
        pauseButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 4. Wait for Mode Indicator to become PAUSED
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("PAUSED") ?? false), UITestTimings.RetryLongTimeout);

        // 5. Click RESUME
        var resumeButton = FindElement(depositWindow, "ResumeDepositButton", "RESUME")?.AsButton();
        resumeButton.ShouldNotBeNull();
        resumeButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 6. Wait for Mode Indicator to return to COUNTING
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), UITestTimings.RetryLongTimeout);
    }

    /// <summary>入金中に返却（RETURN）を選択した場合に、在庫が復元されることを検証する。</summary>
    [Fact]
    public void ShouldRepayDepositWhenReturning()
    {
        var window = _app.MainWindow;
        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        decimal initialTotal = ParseAmount(totalAmountText?.Text ?? "0");

        var depositWindow = OpenDepositTerminal(window);

        // Start Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton?.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Insert
        FillBulkInsert(depositWindow, "1");

        // Click "RETURN"
        var repayButton = FindElement(depositWindow, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.ShouldNotBeNull();
        repayButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Total should stay same
        decimal finalTotal = ParseAmount(totalAmountText?.Text ?? "0");
        finalTotal.ShouldBe(initialTotal);
    }

    /// <summary>紙幣重なりエラーが発生した際に、入金確定がブロックされることを検証する。</summary>
    [Fact]
    public void ShouldPreventFixWhenOverlapped()
    {
        var window = _app.MainWindow;
        var depositWindow = OpenDepositTerminal(window);

        // 1. Begin Deposit
        var beginButton = FindElement(depositWindow, "BeginDepositButton", "START DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 2. Click "ERROR" (Simulate Overlap)
        var overlapButton = UiTestRetry.Find(() => FindElement(depositWindow, "SimulateOverlapButton", null)?.AsButton(), UITestTimings.RetryLongTimeout);
        overlapButton.ShouldNotBeNull();
        overlapButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // 3. Verify VAL ERROR indicator appears (On Main Window or Terminal? Let's assume global status shows it)
        var errorIndicator = UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByText("VAL ERROR")), UITestTimings.RetryLongTimeout);
        errorIndicator.ShouldNotBeNull();

        // 4. Try to click FINISH (FixDeposit) -> Should fail to proceed
        var finishButton = FindElement(depositWindow, "FixDepositButton", "FINISH COUNTING")?.AsButton();
        finishButton.ShouldNotBeNull();
        finishButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Verify VAL ERROR indicator still exists
        window.FindFirstDescendant(cf => cf.ByText("VAL ERROR")).ShouldNotBeNull();

        // 5. Cancel (RETURN) should work and clear error
        var repayButton = FindElement(depositWindow, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.ShouldNotBeNull();
        repayButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Error indicator should be gone
        window.FindFirstDescendant(cf => cf.ByText("VAL ERROR")).ShouldBeNull();
    }

    private Window OpenDepositTerminal(Window mainWindow)
    {
        var launchButton = FindElement(mainWindow, "LaunchDepositButton", "DEPOSIT")?.AsButton();
        launchButton.ShouldNotBeNull();
        
        if (launchButton.Patterns.Invoke.IsSupported)
        {
            launchButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            launchButton.Click();
        }

        Thread.Sleep(UITestTimings.WindowPopupDelayMs);
        var depositWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DepositWindow", UITestTimings.RetryLongTimeout);
        depositWindow.ShouldNotBeNull();
        depositWindow.SetForeground();
        return depositWindow;
    }

    private void FillBulkInsert(Window depositWindow, string quantity)
    {
        var bulkButton = FindElement(depositWindow, "BulkInsertButton", "BULK")?.AsButton();
        bulkButton.ShouldNotBeNull();
        bulkButton.Click();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        // Find the new dialog window
        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", UITestTimings.RetryLongTimeout);
        dialog.ShouldNotBeNull();

        var firstTextBox = UiTestRetry.Find(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("BulkQuantityBox"))?.AsTextBox(), UITestTimings.RetryLongTimeout) as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = quantity;

        var executeButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);
    }

    private static AutomationElement? FindElement(AutomationElement? container, string automationId, string? text)
    {
        if (container == null) return null;
        return UiTestRetry.Find(() =>
        {
            var el = container.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el != null) return el;
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
