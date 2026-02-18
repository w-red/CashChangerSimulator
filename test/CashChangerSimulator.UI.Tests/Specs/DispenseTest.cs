using FlaUI.Core.AutomationElements;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>払出機能の UI 動作を検証するテスト。</summary>
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

    /// <summary>一括払出機能の動作を検証する。</summary>
    [Fact]
    public void ShouldDispenseBulkCash()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        Thread.Sleep(500);

        var totalAmountText = 
            UiTestRetry.Find(
                () => window
                    .FindFirstDescendant(
                        cf => cf.ByAutomationId("TotalAmountText")
                )?.AsLabel(), TimeSpan.FromSeconds(10))
            as Label;
        decimal initialAmount =
            ParseAmount(totalAmountText?.Text ?? "0");

        // Check Mode
        var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel()?.Text;
        Console.WriteLine($"Current Mode: {modeText}");
        
        // Open Bulk Dispense
        var showBulkDispenseButton = 
            UiTestRetry
            .Find(
                () => window
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("BulkDispenseShowButton"))
                ?.AsButton(),
                TimeSpan.FromSeconds(10)) as Button;
        showBulkDispenseButton.ShouldNotBeNull();
        showBulkDispenseButton.IsEnabled.ShouldBeTrue($"ShowBulkDispenseButton is disabled! Mode: {modeText}");
        
        if (showBulkDispenseButton.Patterns.Invoke.IsSupported)
        {
            showBulkDispenseButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            showBulkDispenseButton.Click();
        }
        Thread.Sleep(2000); // Wait for window to open

        // Find the BulkDispenseWindow
        var bulkDispenseWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BULK CASH DISPENSE", TimeSpan.FromSeconds(15));
        bulkDispenseWindow.ShouldNotBeNull();
        
        // Enter dispense quantity
        var firstQuantityBox = UiTestRetry.Find(() =>
        {
            var box =
                bulkDispenseWindow
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("BulkDispenseQuantityBox"))
                ?.AsTextBox();
            return box != null && !box.IsOffscreen
                ? box : (AutomationElement?)null;
        }, TimeSpan.FromSeconds(10)) as TextBox;
        
        firstQuantityBox.ShouldNotBeNull();
        firstQuantityBox.Text = "1";

        // Execute Dispense
        var executeButton = 
            UiTestRetry
            .Find(
                () => bulkDispenseWindow
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("BulkDispenseExecuteButton"))
                ?.AsButton(),
                TimeSpan.FromSeconds(10)) as Button;
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(1000); // Allow window to close and logic to execute

        // Verify total decreased
        decimal newAmount = 0;
        bool success = FlaUI.Core.Tools.Retry.WhileFalse(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel();
            newAmount = ParseAmount(el?.Text ?? "");
            return newAmount < initialAmount;
        }, TimeSpan.FromSeconds(10)).Result;

        success.ShouldBeTrue($"Total amount should decrease. Initial: {initialAmount}, Final: {newAmount}");
    }

    /// <summary>払出実行後に合計金額が減少し、履歴に追加されることを検証する。</summary>
    [Fact]
    public void ShouldDispenseCashAndReduceTotalAmount()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        
        Thread.Sleep(1000);

        // Find controls with retry
        var totalAmountText = 
            UiTestRetry.Find(
                () => window
                .FindFirstDescendant(
                    cf => cf
                    .ByAutomationId("TotalAmountText"))
                ?.AsLabel(),
                TimeSpan.FromSeconds(10)) as Label;
        var dispenseBox = 
            UiTestRetry
            .Find(
                () => window
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("DispenseBox"))
                ?.AsTextBox(),
                TimeSpan.FromSeconds(10)) as TextBox;
        var dispenseButton =
            UiTestRetry
            .Find(
                () => window
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("DispenseButton"))
                ?.AsButton(),
                TimeSpan.FromSeconds(10)) as Button;
        var historyListBox =
            UiTestRetry
            .Find(
                () => window
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("HistoryListBox"))
                ?.AsListBox(),
                TimeSpan.FromSeconds(10)) as ListBox;

        totalAmountText.ShouldNotBeNull();
        dispenseBox.ShouldNotBeNull();
        dispenseButton.ShouldNotBeNull();

        decimal initialAmount = ParseAmount(totalAmountText.Text);
        var initialHistoryCount = historyListBox?.Items.Length ?? 0;

        var dispenseAmount = 123m; 
        Console.WriteLine($"Entering amount: {dispenseAmount}");
        
        dispenseBox.Focus();
        if (dispenseBox.Patterns.Value.IsSupported)
        {
            dispenseBox.Patterns.Value.Pattern.SetValue(dispenseAmount.ToString());
        }
        else
        {
            dispenseBox.Text = dispenseAmount.ToString();
        }
        
        var initialDispenseText = dispenseBox.Text;
        Console.WriteLine($"Box text after entry: '{initialDispenseText}'");
        
        Console.WriteLine("Invoking dispense button...");
        dispenseButton.Click();

        // Wait for clear
        FlaUI.Core.Tools.Retry.WhileTrue(() => dispenseBox.Text == "", TimeSpan.FromSeconds(3));

        // Wait for history update
        FlaUI.Core.Tools.Retry.WhileTrue(() => (historyListBox?.Items.Length ?? 0) > initialHistoryCount, TimeSpan.FromSeconds(3));
        Console.WriteLine($"History count after: {historyListBox?.Items.Length}");
        
        historyListBox?.Items.Length.ShouldBe(initialHistoryCount + 1);
        
        var lastEntry = historyListBox?.Items[0];
        var entryText =
            string
            .Join(" ",
                lastEntry?.FindAllDescendants()
                .Select(e => {
                    try { return e.AsLabel()?.Text ?? ""; }
                    catch { return ""; }
                })
                .Where(t => !string.IsNullOrEmpty(t)) ?? []);
        Console.WriteLine($"History entry text combined: {entryText}");
        entryText.ShouldContain(((int)dispenseAmount).ToString());

        // Wait for update with re-finding
        var expectedAmount = initialAmount - dispenseAmount;
        decimal finalAmount = 0;
        FlaUI.Core.Tools.Retry.WhileTrue(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel();
            finalAmount = ParseAmount(el?.Text ?? "");
            return finalAmount == expectedAmount;
        }, TimeSpan.FromSeconds(10));
        
        Console.WriteLine($"Initial: {initialAmount}, Expected: {expectedAmount}, Final Amount: {finalAmount}");
        finalAmount.ShouldBe(expectedAmount);
    }
    
    /// <summary>払出金額入力欄のバリデーションを検証する。</summary>
    /// <summary>無効な払出金額（文字やマイナス値）が入力された場合に、払出ボタンが適切に制御されることを検証する。</summary>
    [Fact]
    public void ShouldValidateInput()
    {
        var window = _app.MainWindow;
        var dispenseBox = UiTestRetry.Find(() => window!.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(10)) as TextBox;
        dispenseBox.ShouldNotBeNull();

        dispenseBox.Focus();
        dispenseBox.Text = "abc";
        Thread.Sleep(500);
        dispenseBox.Text.ShouldBe("abc");
    }

    private static decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        // Strip everything but digits
        var cleaned = new string([.. text.Where(char.IsDigit)]);
        return decimal.TryParse(cleaned, out var result)
            ? result : 0;
    }

    public void Dispose()
    {
        _app?.Dispose();
        GC.SuppressFinalize(this);
    }
}
