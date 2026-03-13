using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>出金フロー（通常出金、一括出金）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class DispenseTest : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリのフィクスチャを受け取り、初期状態をセットアップする。</summary>
    public DispenseTest(CashChangerTestApp app)
    {
        _app = app;
    }

    /// <summary>一括出金（Bulk Dispense）機能の正常系フローを検証する。</summary>
    [Fact]
    public void ShouldCompleteBulkDispenseFlow()
    {
        _app.Launch();
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();

        // 1. Get initial total (Main Window)
        var totalAmountLabel = FindElement(window, "TotalAmountText", "\\")?.AsLabel();
        totalAmountLabel.ShouldNotBeNull();
        decimal initialAmount = ParseAmount(totalAmountLabel.Text);

        // 2. Open Dispense Terminal
        var dispenseWindow = OpenDispenseTerminal(window);

        // 3. Open Bulk Dispense Window
        var showBulkButton = FindElement(dispenseWindow, "BulkDispenseShowButton", "BULK")?.AsButton();
        showBulkButton.SmartClick();
        Thread.Sleep(UITestTimings.WindowPopupDelayMs);

        // Find the new dialog window
        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", UITestTimings.RetryLongTimeout);
        dialog.ShouldNotBeNull();

        // 4. Enter quantities
        var firstQuantityBox = UiTestRetry.Find(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("BulkQuantityBox"))?.AsTextBox(), UITestTimings.RetryLongTimeout) as TextBox;
        firstQuantityBox.ShouldNotBeNull();
        firstQuantityBox.Text = "1";

        // 5. Execute
        var executeButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        executeButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // 6. Verify total decreased (Check on Main Window)
        Retry.WhileTrue(() => ParseAmount(totalAmountLabel.Text) == initialAmount, UITestTimings.RetryLongTimeout);
        decimal finalAmount = ParseAmount(totalAmountLabel.Text);
        finalAmount.ShouldBeLessThan(initialAmount);
    }

    /// <summary>金額指定による通常出金フローを検証する。</summary>
    [Fact]
    public void ShouldPerformSimpleDispense()
    {
        _app.Launch();
        var window = _app.MainWindow;
        var dispenseWindow = OpenDispenseTerminal(window);

        // 1. Enter amount
        var dispenseBox = FindElement(dispenseWindow, "DispenseBox", null)?.AsTextBox();
        dispenseBox.ShouldNotBeNull();
        dispenseBox.Text = "1000";

        // 2. Click Dispense
        var dispenseButton = FindElement(dispenseWindow, "DispenseButton", null)?.AsButton();
        dispenseButton.SmartClick();

        // 3. Verify Busy state appears on terminal
        var busyIndicator = UiTestRetry.Find(() => dispenseWindow.FindFirstDescendant(cf => cf.ByText("DISPENSING...")), UITestTimings.RetryShortTimeout);
        busyIndicator.ShouldNotBeNull();

        // Wait for completion (Return to Idle)
        Retry.WhileTrue(() => dispenseWindow.FindFirstDescendant(cf => cf.ByText("DISPENSING...")) != null, UITestTimings.RetryLongTimeout);
    }

    /// <summary>エラー状態（ジャム等）の際にコントロールが無効化されることを検証する。</summary>
    [Fact]
    public void ShouldDisableControlsWhenErrorOccurs()
    {
        _app.Launch();
        var window = _app.MainWindow;
        var dispenseWindow = OpenDispenseTerminal(window);

        // Verify initial state
        var dispenseBox = FindElement(dispenseWindow, "DispenseBox", null)?.AsTextBox();
        var dispenseButton = FindElement(dispenseWindow, "DispenseButton", null)?.AsButton();
        var showBulkButton = FindElement(dispenseWindow, "BulkDispenseShowButton", null)?.AsButton();

        dispenseBox.ShouldNotBeNull();
        dispenseButton.ShouldNotBeNull();
        showBulkButton.ShouldNotBeNull();

        // Enter amount so DispenseButton evaluates CanExecute to true
        dispenseBox.Text = "1000";
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        dispenseBox.IsEnabled.ShouldBeTrue();
        dispenseButton.IsEnabled.ShouldBeTrue();
        showBulkButton.IsEnabled.ShouldBeTrue();

        // Simulate Jam
        var simulateJamButton = FindElement(dispenseWindow, "SimulateJamButton", null)?.AsButton();
        simulateJamButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Verify disabled state
        dispenseBox.IsEnabled.ShouldBeFalse();
        dispenseButton.IsEnabled.ShouldBeFalse();
        showBulkButton.IsEnabled.ShouldBeFalse();

        // Reset Error
        var resetErrorButton = FindElement(dispenseWindow, "ResetErrorButton", null)?.AsButton();
        resetErrorButton.SmartClick();
        Thread.Sleep(UITestTimings.UiTransitionDelayMs);

        // Verify enabled state
        dispenseBox.IsEnabled.ShouldBeTrue();
        dispenseButton.IsEnabled.ShouldBeTrue();
        showBulkButton.IsEnabled.ShouldBeTrue();
    }

    private Window OpenDispenseTerminal(Window? mainWindow)
    {
        var launchButton = UiTestRetry.Find(() => mainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDispenseButton"))?.AsButton(), UITestTimings.RetryLongTimeout);
        launchButton.SmartClick();

        Thread.Sleep(UITestTimings.WindowPopupDelayMs);
        var dispenseWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DispenseWindow", UITestTimings.RetryLongTimeout);
        dispenseWindow.ShouldNotBeNull();
        dispenseWindow.SetForeground();
        return dispenseWindow;
    }

    private static AutomationElement? FindElement(AutomationElement? container, string automationId, string? text)
    {
        return container == null
            ? null
            : UiTestRetry.Find(() =>
        {
            // Try by AutomationId first (highest priority)
            var el = container.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el != null) return el;

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

}
