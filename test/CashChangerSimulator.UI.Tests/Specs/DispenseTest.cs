using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>出金フロー（通常出金、一括出金）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class DispenseTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリを起動し、初期状態をセットアップする。</summary>
    public DispenseTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    /// <summary>一括出金（Bulk Dispense）機能の正常系フローを検証する。</summary>
    [Fact]
    public void ShouldCompleteBulkDispenseFlow()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();

        // 1. Get initial total (Main Window)
        var totalAmountLabel = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        totalAmountLabel.ShouldNotBeNull();
        decimal initialAmount = ParseAmount(totalAmountLabel.Text);

        // 2. Open Dispense Terminal
        var dispenseWindow = OpenDispenseTerminal(window);

        // 3. Open Bulk Dispense Window
        var showBulkButton = FindElement(dispenseWindow, "BulkDispenseShowButton", "BULK")?.AsButton();
        showBulkButton.ShouldNotBeNull();
        showBulkButton.Click();
        Thread.Sleep(DepositTestTimings.WindowPopupDelayMs);

        // Find the new dialog window
        var dialog = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BulkAmountInputWindow", TimeSpan.FromSeconds(15));
        dialog.ShouldNotBeNull();

        // 4. Enter quantities
        var firstQuantityBox = UiTestRetry.Find(() => dialog.FindFirstDescendant(cf => cf.ByAutomationId("BulkQuantityBox"))?.AsTextBox(), TimeSpan.FromSeconds(10)) as TextBox;
        firstQuantityBox.ShouldNotBeNull();
        firstQuantityBox.Text = "1";

        // 5. Execute
        var executeButton = FindElement(dialog, "BulkConfirmButton", "OK")?.AsButton();
        executeButton.ShouldNotBeNull();
        if (executeButton.Patterns.Invoke.IsSupported)
        {
            executeButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            executeButton.Click();
        }
        Thread.Sleep(DepositTestTimings.LogicExecutionDelayMs);

        // 6. Verify total decreased (Check on Main Window)
        Retry.WhileTrue(() => ParseAmount(totalAmountLabel.Text) == initialAmount, TimeSpan.FromSeconds(10));
        decimal finalAmount = ParseAmount(totalAmountLabel.Text);
        finalAmount.ShouldBeLessThan(initialAmount);
    }

    /// <summary>金額指定による通常出金フローを検証する。</summary>
    [Fact]
    public void ShouldPerformSimpleDispense()
    {
        var window = _app.MainWindow;
        var dispenseWindow = OpenDispenseTerminal(window);

        // 1. Enter amount
        var dispenseBox = FindElement(dispenseWindow, "DispenseBox", null)?.AsTextBox();
        dispenseBox.ShouldNotBeNull();
        dispenseBox.Text = "1000";

        // 2. Click Dispense
        var dispenseButton = FindElement(dispenseWindow, "DispenseButton", null)?.AsButton();
        dispenseButton.ShouldNotBeNull();
        if (dispenseButton.Patterns.Invoke.IsSupported)
        {
            dispenseButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            dispenseButton.Click();
        }

        // 3. Verify Busy state appears on terminal
        var busyIndicator = UiTestRetry.Find(() => dispenseWindow.FindFirstDescendant(cf => cf.ByText("DISPENSING...")), TimeSpan.FromSeconds(5));
        busyIndicator.ShouldNotBeNull();
        
        // Wait for completion (Return to Idle)
        Retry.WhileTrue(() => dispenseWindow.FindFirstDescendant(cf => cf.ByText("DISPENSING...")) != null, TimeSpan.FromSeconds(10));
    }

    private Window OpenDispenseTerminal(Window mainWindow)
    {
        var launchButton = FindElement(mainWindow, "LaunchDispenseButton", "DISPENSE")?.AsButton();
        launchButton.ShouldNotBeNull();
        
        if (launchButton.Patterns.Invoke.IsSupported)
        {
            launchButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            launchButton.Click();
        }

        Thread.Sleep(DepositTestTimings.WindowPopupDelayMs);
        var dispenseWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DispenseWindow", TimeSpan.FromSeconds(15));
        dispenseWindow.ShouldNotBeNull();
        dispenseWindow.SetForeground();
        return dispenseWindow;
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
        }, TimeSpan.FromSeconds(10));
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
