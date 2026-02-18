using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>入金フロー（開始、投入、確定、返却）を検証する UI テスト。</summary>
[Collection("SequentialTests")]
public class DepositTest : IDisposable
{
    private readonly CashChangerTestApp _app;

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
        Thread.Sleep(1000);

        // 1. Get initial total
        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        totalAmountText.ShouldNotBeNull();
        decimal initialTotal = ParseAmount(totalAmountText.Text);

        // 2. Click "NEW DEPOSIT"
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(2000); // Allow UI to transition and elements to appear in tree

        // 3. Add some cash
        // 3. Add some cash via Bulk Insert
        var bulkButton = FindElement(window, "BulkInsertButton", "BULK INSERT")?.AsButton();
        bulkButton.ShouldNotBeNull();
        bulkButton.Click();
        Thread.Sleep(1000);

        var firstTextBox = (TextBox)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("BulkInsertQuantityBox"))?.AsTextBox(), TimeSpan.FromSeconds(10));
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "2";

        var executeButton = FindElement(window, "BulkInsertExecuteButton", "INSERT ALL")?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(500);

        // 4. Verify Current Deposit Amount updates
        var currentDepositText = FindElement(window, "CurrentDepositText", null)?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        FlaUI.Core.Tools.Retry.WhileTrue(() => ParseAmount(currentDepositText.Text ?? "0") == 0, TimeSpan.FromSeconds(10));
        decimal deposited = ParseAmount(currentDepositText.Text ?? "0");
        deposited.ShouldBeGreaterThan(0);

        // 5. Click "FINISH"
        var fixButton = FindElement(window, "FixDepositButton", "FINISH")?.AsButton();
        fixButton.ShouldNotBeNull();
        fixButton.Click();
        Thread.Sleep(500); // Allow mode transition

        // 6. Click "STORE"
        var storeButton = FindElement(window, "StoreDepositButton", "STORE")?.AsButton();
        storeButton.ShouldNotBeNull();
        storeButton.Click();

        // 7. Verify Global Total updated
        FlaUI.Core.Tools.Retry.WhileTrue(() => ParseAmount(totalAmountText.Text) == initialTotal, TimeSpan.FromSeconds(10));
        decimal finalTotal = ParseAmount(totalAmountText.Text);
        finalTotal.ShouldBe(initialTotal + deposited);
    }

    /// <summary>バルク投入ダイアログの動作を検証する。</summary>
    [Fact]
    public void ShouldInsertBulkCash()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        
        // Start Deposit
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton?.Click();
        
        // Wait for state transition to Deposit Mode
        Thread.Sleep(500); 

        // Open Bulk Insert Dialog
        var bulkButton = FindElement(window, "BulkInsertButton", "BULK INSERT")?.AsButton();
        if (bulkButton == null)
        {
            // Second attempt with a refresh/longer wait
            Thread.Sleep(1000);
            bulkButton = FindElement(window, "BulkInsertButton", "BULK INSERT")?.AsButton();
        }
        bulkButton.ShouldNotBeNull();
        bulkButton.Click();
        Thread.Sleep(1000); // Wait for Dialog animation

        // Find the first TextBox
        var firstTextBox = (TextBox)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("BulkInsertQuantityBox"))?.AsTextBox(), TimeSpan.FromSeconds(10));
        if (firstTextBox == null)
        {
            // Fallback: If not found, it might be due to timing - but we already have 10s wait in UiTestRetry.Find
        }
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "10";

        // Click "INSERT ALL"
        var executeButton = FindElement(window, "BulkInsertExecuteButton", "INSERT ALL")?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();

        // Verify Current Deposit Amount
        var currentDepositText = FindElement(window, "CurrentDepositText", null)?.AsLabel();
        FlaUI.Core.Tools.Retry.WhileTrue(() => ParseAmount(currentDepositText?.Text ?? "0") == 0, TimeSpan.FromSeconds(5));
        ParseAmount(currentDepositText?.Text ?? "0").ShouldBeGreaterThan(0);
    }

    /// <summary>入金中の一時停止および再開動作、モード表示の遷移を検証する。</summary>
    [Fact]
    public void ShouldPauseAndResumeDeposit()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();

        // 1. Start Deposit
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();

        // 2. Wait for Mode Indicator to become COUNTING
        var modeIndicator = FindElement(window, "ModeIndicatorText", null)?.AsLabel();
        modeIndicator.ShouldNotBeNull();
        FlaUI.Core.Tools.Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("COUNTING");

        // 3. Click PAUSE
        var pauseButton = (Button)UiTestRetry.Find(() => FindElement(window, "PauseDepositButton", "PAUSE")?.AsButton(), TimeSpan.FromSeconds(10));
        pauseButton.ShouldNotBeNull();
        pauseButton.Click();
        Thread.Sleep(500);

        // 4. Wait for Mode Indicator to become PAUSED
        FlaUI.Core.Tools.Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("PAUSED") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("PAUSED");

        // 5. Click RESUME
        var resumeButton = (Button)UiTestRetry.Find(() => FindElement(window, "ResumeDepositButton", "RESUME")?.AsButton(), TimeSpan.FromSeconds(10));
        resumeButton.ShouldNotBeNull();
        resumeButton.Click();
        Thread.Sleep(500);

        // 6. Wait for Mode Indicator to return to COUNTING
        FlaUI.Core.Tools.Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("COUNTING");
    }

    /// <summary>入金途中に「RETURN」をクリックして、在庫が増えないことを検証する。</summary>
    [Fact]
    public void ShouldRepayDepositWhenReturning()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();

        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        decimal initialTotal = ParseAmount(totalAmountText?.Text ?? "0");

        // Start Deposit
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton?.Click();

        // Add some cash
        // Add some cash via Bulk Insert
        Thread.Sleep(500);
        var bulkButton = FindElement(window, "BulkInsertButton", "BULK INSERT")?.AsButton();
        if (bulkButton != null)
        {
            bulkButton.Click();
            Thread.Sleep(1000);
            var firstTextBox = (TextBox)UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("BulkInsertQuantityBox"))?.AsTextBox(), TimeSpan.FromSeconds(5));
            if (firstTextBox != null)
            {
                firstTextBox.Text = "1";
                var executeButton = FindElement(window, "BulkInsertExecuteButton", "INSERT ALL")?.AsButton();
                executeButton?.Click();
                Thread.Sleep(500);
            }
        }

        // Click "RETURN"
        var repayButton = FindElement(window, "RepayDepositButton", "RETURN")?.AsButton();
        repayButton.ShouldNotBeNull();
        repayButton.Click();

        // Total should stay same
        Thread.Sleep(1000);
        decimal finalTotal = ParseAmount(totalAmountText?.Text ?? "0");
        finalTotal.ShouldBe(initialTotal);
    }

    private AutomationElement? FindElement(Window? window, string automationId, string? text)
    {
        if (window == null) return null;
        var result = UiTestRetry.Find(() => {
            var el = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el != null) return el;
            if (!string.IsNullOrEmpty(text))
            {
                var elByText = window.FindFirstDescendant(cf => cf.ByText(text));
                if (elByText != null) return elByText;
            }
            return null;
        }, TimeSpan.FromSeconds(10));

        return result;
    }

    private decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
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
