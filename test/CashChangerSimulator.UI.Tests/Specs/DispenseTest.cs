using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;
using System.Threading;

namespace CashChangerSimulator.UI.Tests.Specs;

public class DispenseTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    public DispenseTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    [Fact]
    public void ShouldIncreaseAmountWhenClickingAddButton()
    {
        var window = _app.MainWindow;
        Assert.NotNull(window);
        Thread.Sleep(1000);

        var totalAmountText = (Label)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel(), TimeSpan.FromSeconds(5));
        Assert.NotNull(totalAmountText);

        decimal initialAmount = ParseAmount(totalAmountText.Text);
        
        // Find the "+" button
        var addButton = (Button)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByName("+"))?.AsButton(), TimeSpan.FromSeconds(5));
        Assert.NotNull(addButton);

        Console.WriteLine($"Initial Total: {initialAmount} (Raw: '{totalAmountText.Text}')");
        addButton.Click();
        
        decimal newAmount = 0;
        FlaUI.Core.Tools.Retry.WhileTrue(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel();
            newAmount = ParseAmount(el?.Text ?? "");
            return newAmount > initialAmount;
        }, TimeSpan.FromSeconds(10));

        Console.WriteLine($"After '+' click: Total={newAmount}");
        Assert.True(newAmount > initialAmount, $"Global total should increase. Initial: {initialAmount}, Final: {newAmount}");
    }

    [Fact]
    public void ShouldDispenseCashAndReduceTotalAmount()
    {
        var window = _app.MainWindow;
        Assert.NotNull(window);
        
        Thread.Sleep(1000);

        // Find controls with retry
        var totalAmountText = (Label)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel(), TimeSpan.FromSeconds(5));
        var dispenseBox = (TextBox)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(5));
        var dispenseButton = (Button)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DispenseButton"))?.AsButton(), TimeSpan.FromSeconds(5));
        var historyListBox = (ListBox)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("HistoryListBox"))?.AsListBox(), TimeSpan.FromSeconds(5));

        Assert.NotNull(totalAmountText);
        Assert.NotNull(dispenseBox);
        Assert.NotNull(dispenseButton);

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
        Assert.Equal(initialHistoryCount + 1, historyListBox?.Items.Length);
        
        var lastEntry = historyListBox?.Items[0];
        var entryText = string.Join(" ", lastEntry?.FindAllDescendants().Select(e => {
            try { return e.AsLabel()?.Text ?? ""; } catch { return ""; }
        }).Where(t => !string.IsNullOrEmpty(t)) ?? Array.Empty<string>());
        Console.WriteLine($"History entry text combined: {entryText}");
        Assert.Contains(((int)dispenseAmount).ToString(), entryText);

        // Wait for update with re-finding
        var expectedAmount = initialAmount - dispenseAmount;
        decimal finalAmount = 0;
        FlaUI.Core.Tools.Retry.WhileTrue(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel();
            finalAmount = ParseAmount(el?.Text ?? "");
            return finalAmount == expectedAmount;
        }, TimeSpan.FromSeconds(10));
        
        Console.WriteLine($"Initial: {initialAmount}, Expected: {expectedAmount}, Final Amount: {finalAmount}");
        Assert.Equal(expectedAmount, finalAmount);
    }
    
    [Fact]
    public void ShouldValidateInput()
    {
        var window = _app.MainWindow;
        var dispenseBox = (TextBox)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(5));
        Assert.NotNull(dispenseBox);

        dispenseBox.Focus();
        dispenseBox.Text = "abc";
        Thread.Sleep(500);
        Assert.Equal("abc", dispenseBox.Text);
    }

    private decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Strip everything but digits
        var cleaned = new string(text.Where(char.IsDigit).ToArray());
        if (decimal.TryParse(cleaned, out var result))
            return result;
        return 0;
    }

    public void Dispose()
    {
        _app?.Dispose();
    }
}

public static class Retry
{
    public static AutomationElement Find(Func<AutomationElement?> findFunc, TimeSpan timeout)
    {
        AutomationElement? result = null;
        FlaUI.Core.Tools.Retry.WhileTrue(() => {
            result = findFunc();
            return result != null;
        }, timeout);
        return result!;
    }
}
