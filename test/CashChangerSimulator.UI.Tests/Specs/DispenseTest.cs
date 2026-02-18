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

        var totalAmountText = (Label)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel(), TimeSpan.FromSeconds(10));
        decimal initialAmount = ParseAmount(totalAmountText?.Text ?? "0");

        // Check Mode
        var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel()?.Text;
        Console.WriteLine($"Current Mode: {modeText}");
        
        // Open Bulk Dispense
        var showBulkDispenseButton = (Button)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("BulkDispenseShowButton"))?.AsButton(), TimeSpan.FromSeconds(10));
        showBulkDispenseButton.ShouldNotBeNull();
        showBulkDispenseButton.IsEnabled.ShouldBeTrue($"ShowBulkDispenseButton is disabled! Mode: {modeText}");
        
        showBulkDispenseButton.Click();
        Thread.Sleep(2000); // Increased wait for dialog animation

        // Check if Dialog Title exists
        var dialogTitle = UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByText("BULK DISPENSE")), TimeSpan.FromSeconds(30));
        dialogTitle.ShouldNotBeNull();

        // Enter dispense quantity
        var firstQuantityBox = (TextBox)UiTestRetry.Find(() => {
            var box = window.FindFirstDescendant(cf => cf.ByAutomationId("BulkDispenseQuantityBox"))?.AsTextBox();
            if (box != null && !box.IsOffscreen) return box;
            return null;
        }, TimeSpan.FromSeconds(10));
        
        if (firstQuantityBox == null)
        {
             Console.WriteLine("Could not find BulkDispenseQuantityBox. Dumping window descendants:");
             // (Optional: dump tree)
        }
        firstQuantityBox.ShouldNotBeNull();
        firstQuantityBox.Text = "1";

        // Execute Dispense
        var executeButton = (Button)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("BulkDispenseExecuteButton"))?.AsButton(), TimeSpan.FromSeconds(10));
        executeButton.ShouldNotBeNull();
        executeButton.Click();

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
        var totalAmountText = (Label)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel(), TimeSpan.FromSeconds(10));
        var dispenseBox = (TextBox)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(10));
        var dispenseButton = (Button)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DispenseButton"))?.AsButton(), TimeSpan.FromSeconds(10));
        var historyListBox = (ListBox)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("HistoryListBox"))?.AsListBox(), TimeSpan.FromSeconds(10));

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
        var entryText = string.Join(" ", lastEntry?.FindAllDescendants().Select(e => {
            try { return e.AsLabel()?.Text ?? ""; } catch { return ""; }
        }).Where(t => !string.IsNullOrEmpty(t)) ?? Array.Empty<string>());
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
    [Fact]
    public void ShouldValidateInput()
    {
        var window = _app.MainWindow;
        var dispenseBox = (TextBox)UiTestRetry.Find(() => window!.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(10));
        dispenseBox.ShouldNotBeNull();

        dispenseBox.Focus();
        dispenseBox.Text = "abc";
        Thread.Sleep(500);
        dispenseBox.Text.ShouldBe("abc");
    }

    private decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Strip everything but digits
        var cleaned = new string([.. text.Where(char.IsDigit)]);
        if (decimal.TryParse(cleaned, out var result))
            return result;
        return 0;
    }

    public void Dispose()
    {
        _app?.Dispose();
    }
}
