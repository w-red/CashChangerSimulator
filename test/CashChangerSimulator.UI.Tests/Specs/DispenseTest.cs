using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;
using System.Threading;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>払出機能の UI 動作を検証するテスト。</summary>
public class DispenseTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリを起動し、初期状態をセットアップする。</summary>
    public DispenseTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    /// <summary>Add ボタンクリック時に合計金額が増加することを検証する。</summary>
    [Fact]
    public void ShouldIncreaseAmountWhenClickingAddButton()
    {
        var window = _app.MainWindow;
        Assert.NotNull(window);
        window.SetForeground();
        Thread.Sleep(500);

        var totalAmountText = (Label)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel(), TimeSpan.FromSeconds(5));
        Assert.NotNull(totalAmountText);

        decimal initialAmount = ParseAmount(totalAmountText.Text);
        
        // Find the "+" button by AutomationId
        var addButton = (Button)Retry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("AddButton"))?.AsButton(), TimeSpan.FromSeconds(5));
        Assert.NotNull(addButton);

        Console.WriteLine($"Initial Total: {initialAmount} (Raw: '{totalAmountText.Text}')");
        addButton.Click();
        
        decimal newAmount = 0;
        bool success = FlaUI.Core.Tools.Retry.WhileFalse(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId("TotalAmountText"))?.AsLabel();
            newAmount = ParseAmount(el?.Text ?? "");
            return newAmount > initialAmount;
        }, TimeSpan.FromSeconds(10)).Result;

        Console.WriteLine($"After '+' click: Success={success}, Total={newAmount}");
        Assert.True(newAmount > initialAmount, $"Global total should increase. Initial: {initialAmount}, Final: {newAmount}");
    }

    /// <summary>払出実行後に合計金額が減少し、履歴に追加されることを検証する。</summary>
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
    
    /// <summary>払出金額入力欄のバリデーションを検証する。</summary>
    [Fact]
    public void ShouldValidateInput()
    {
        var window = _app.MainWindow;
        var dispenseBox = (TextBox)Retry.Find(() => window!.FindFirstDescendant(cf => cf.ByAutomationId("DispenseBox"))?.AsTextBox(), TimeSpan.FromSeconds(5));
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
