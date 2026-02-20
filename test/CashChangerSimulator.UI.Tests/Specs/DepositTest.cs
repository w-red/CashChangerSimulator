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
        VerifyComponentStructure(window);
        Thread.Sleep(1000);


        // 1. Get initial total
        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        totalAmountText.ShouldNotBeNull();
        decimal initialTotal = ParseAmount(totalAmountText.Text);

        // 2. Click "NEW DEPOSIT"
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        // 3. Bulk Insert Window should open automatically
        // var bulkButton = FindElement(window, "BulkInsertButton", "BULK INSERT")?.AsButton();
        // bulkButton.ShouldNotBeNull();
        // bulkButton.Click();
        Thread.Sleep(1000);

        // Find the BulkInsertWindow
        var bulkInsertWindow =
            UiTestRetry
            .FindWindow(
                _app.Application,
                _app.Automation,
                "BULK CASH INSERT",
                TimeSpan.FromSeconds(15));
        bulkInsertWindow.ShouldNotBeNull();

        var firstTextBox =
            UiTestRetry
            .Find(
                () => bulkInsertWindow
                    .FindFirstDescendant(
                        cf => cf.ByAutomationId("BulkInsertQuantityBox")
                )?.AsTextBox(),
                TimeSpan.FromSeconds(10))
            as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "2";

        var executeButton = FindElement(bulkInsertWindow, "BulkInsertExecuteButton", "INSERT ALL")?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(1000); // Allow window to close and logic to execute

        // 4. Verify Current Deposit Amount updates
        var currentDepositText = FindElement(window, "CurrentDepositText", null)?.AsLabel();
        currentDepositText.ShouldNotBeNull();
        Retry.WhileTrue(() => ParseAmount(currentDepositText.Text ?? "0") == 0, TimeSpan.FromSeconds(10));
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
        Retry.WhileTrue(() => ParseAmount(totalAmountText.Text) == initialTotal, TimeSpan.FromSeconds(10));
        decimal finalTotal = ParseAmount(totalAmountText.Text);
        finalTotal.ShouldBe(initialTotal + deposited);
    }

    /// <summary>バルク投入ダイアログの動作を検証する。</summary>
    /// <summary>一括投入（Bulk Insert）機能の動作を検証する。</summary>
    [Fact]
    public void ShouldInsertBulkCash()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        VerifyComponentStructure(window);


        // Start Deposit
        var beginButton =
            FindElement(
                window,
                "BeginDepositButton",
                "NEW DEPOSIT")
            ?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton?.Click();

        // Wait for state transition to Deposit Mode
        // Check for automatic BulkInsertWindow and proceed
        var bulkInsertWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BULK CASH INSERT", TimeSpan.FromSeconds(10));
        bulkInsertWindow.ShouldNotBeNull();


        // Find the first TextBox
        var firstTextBox =
            UiTestRetry.Find(
                () => bulkInsertWindow
                .FindFirstDescendant(
                    cf => cf.ByAutomationId("BulkInsertQuantityBox"))
                ?.AsTextBox(), TimeSpan.FromSeconds(10)) as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "10";

        // Click "INSERT ALL"
        var executeButton = FindElement(bulkInsertWindow, "BulkInsertExecuteButton", "INSERT ALL")?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(1000); // Wait for logic and window close

        // Verify Current Deposit Amount
        var currentDepositText = FindElement(window, "CurrentDepositText", null)?.AsLabel();
        Retry.WhileTrue(() => ParseAmount(currentDepositText?.Text ?? "0") == 0, TimeSpan.FromSeconds(5));
        ParseAmount(currentDepositText?.Text ?? "0").ShouldBeGreaterThan(0);
    }

    /// <summary>入金中の一時停止および再開動作、モード表示の遷移を検証する。</summary>
    [Fact]
    public void ShouldPauseAndResumeDeposit()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        VerifyComponentStructure(window);


        // 1. Start Deposit
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(1000);

        // Close the Bulk Insert Window that opens automatically
        var bulkInsertWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BULK CASH INSERT", TimeSpan.FromSeconds(5));
        if (bulkInsertWindow != null)
        {
            var cancelButton = FindElement(bulkInsertWindow, "BulkInsertCancelButton", "CANCEL")?.AsButton();
            cancelButton?.Click();
            Thread.Sleep(500);
        }

        // 2. Wait for Mode Indicator to become COUNTING
        var modeIndicator = FindElement(window, "ModeIndicatorText", null)?.AsLabel();
        modeIndicator.ShouldNotBeNull();
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("COUNTING");

        // 3. Click PAUSE
        var pauseButton =
            UiTestRetry
            .Find(
                () => FindElement(
                    window,
                    "PauseDepositButton",
                    "PAUSE")
                ?.AsButton(),
                TimeSpan.FromSeconds(10)) as Button;
        pauseButton.ShouldNotBeNull();
        pauseButton.Click();
        Thread.Sleep(500);

        // 4. Wait for Mode Indicator to become PAUSED
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("PAUSED") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("PAUSED");

        // 5. Click RESUME
        var resumeButton =
            UiTestRetry
            .Find(
                () => FindElement(
                    window,
                    "ResumeDepositButton",
                    "RESUME")?.AsButton(),
                TimeSpan.FromSeconds(10)) as Button;
        resumeButton.ShouldNotBeNull();
        resumeButton.Click();
        Thread.Sleep(500);

        // 6. Wait for Mode Indicator to return to COUNTING
        Retry.WhileTrue(() => !(modeIndicator.Text?.Contains("COUNTING") ?? false), TimeSpan.FromSeconds(10));
        modeIndicator.Text.ShouldContain("COUNTING");
    }

    /// <summary>入金途中に「RETURN」をクリックして、在庫が増えないことを検証する。</summary>
    /// <summary>入金中に返却（RETURN）を選択した場合に、在庫が復元されることを検証する。</summary>
    [Fact]
    public void ShouldRepayDepositWhenReturning()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        VerifyComponentStructure(window);


        var totalAmountText = FindElement(window, "TotalAmountText", "¥")?.AsLabel();
        decimal initialTotal = ParseAmount(totalAmountText?.Text ?? "0");

        // Start Deposit
        var beginButton = FindElement(window, "BeginDepositButton", "NEW DEPOSIT")?.AsButton();
        beginButton?.Click();

        // Find the BulkInsertWindow
        var bulkInsertWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BULK CASH INSERT", TimeSpan.FromSeconds(10));
        bulkInsertWindow.ShouldNotBeNull();

        var firstTextBox =
            UiTestRetry
            .Find(
                    () => bulkInsertWindow
                    .FindFirstDescendant(
                        cf => cf.ByAutomationId("BulkInsertQuantityBox"))
                    ?.AsTextBox(),
                    TimeSpan.FromSeconds(5)) as TextBox;
        firstTextBox.ShouldNotBeNull();
        firstTextBox.Text = "1";
        var executeButton =
            FindElement(
                bulkInsertWindow,
                "BulkInsertExecuteButton",
                "INSERT ALL")
            ?.AsButton();
        executeButton.ShouldNotBeNull();
        executeButton.Click();
        Thread.Sleep(500);


        // Click "RETURN"
        var repayButton =
            FindElement(
                window,
                "RepayDepositButton",
                "RETURN")
            ?.AsButton();
        repayButton.ShouldNotBeNull();
        repayButton.Click();

        // Total should stay same
        Thread.Sleep(1000);
        decimal finalTotal =
            ParseAmount(
                totalAmountText?.Text ?? "0");
        finalTotal.ShouldBe(initialTotal);
    }

    /// <summary>紙幣重なりエラーが発生した際に、入金確定がブロックされることを検証する。</summary>
    [Fact]
    public void ShouldPreventFixWhenOverlapped()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        VerifyComponentStructure(window);
        Thread.Sleep(500);


        // 1. Begin Deposit
        var beginButton =
            FindElement(
                window,
                "BeginDepositButton",
                "NEW DEPOSIT")
            ?.AsButton();
        beginButton.ShouldNotBeNull();
        beginButton.Click();
        Thread.Sleep(1000);

        // Close the Bulk Insert Window that opens automatically
        var bulkInsertWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "BULK CASH INSERT", TimeSpan.FromSeconds(10));
        if (bulkInsertWindow != null)
        {
            var cancelButton = FindElement(bulkInsertWindow, "BulkInsertCancelButton", "CANCEL")?.AsButton();
            cancelButton?.Click();
            Thread.Sleep(1000);
        }

        window.SetForeground();
        Thread.Sleep(500);

        // 2. Click "SIMULATE OVERLAP"
        var overlapButton =
            FindElement(
                window,
                "SimulateOverlapButton",
                "SIMULATE OVERLAP")
            ?.AsButton();

        if (overlapButton == null)
        {
            DumpDescendants(window);
        }

        overlapButton.ShouldNotBeNull();
        overlapButton.Click();

        Thread.Sleep(1000);

        // 3. Verify VAL ERROR indicator appears
        var errorIndicator =
            window
            .FindFirstDescendant(
                cf => cf.ByText("VAL ERROR"));
        errorIndicator.ShouldNotBeNull();
        errorIndicator.IsOffscreen.ShouldBeFalse();

        // 4. Try to click FINISH (FixDeposit) -> Should fail or show error
        var finishButton =
            window
            .FindFirstDescendant(
                cf => cf.ByAutomationId("FixDepositButton"));
        finishButton.ShouldNotBeNull();
        finishButton.Click();
        Thread.Sleep(500);

        // Verify VAL ERROR indicator still exists
        window
            .FindFirstDescendant(cf => cf.ByText("VAL ERROR"))
            .ShouldNotBeNull();

        // 5. Cancel (RETURN) should work and clear error
        var repayButton = FindElement(
                window,
                "RepayDepositButton",
                "RETURN")
            ?.AsButton();

        repayButton.ShouldNotBeNull();
        repayButton.Click();
        Thread.Sleep(1000);

        // Error indicator should be gone
        window
            .FindFirstDescendant(
            cf => cf.ByText("VAL ERROR"))
            .ShouldBeNull();
    }

    /// <summary>指定した AutomationId またはテキストを持つ要素を、リトライしながら検索するヘルパーメソッド。</summary>
    private static void VerifyComponentStructure(Window window)
    {
        // These should exist after refactoring as UserControl containers
        FindElement(window, "DepositComponent", null).ShouldNotBeNull();
        FindElement(window, "DispenseComponent", null).ShouldNotBeNull();
        FindElement(window, "InventoryComponent", null).ShouldNotBeNull();
    }

    private static AutomationElement? FindElement(Window? window, string automationId, string? text)

    {
        if (window == null) return null;
        var result = UiTestRetry.Find(() =>
        {
            var el = window
                .FindFirstDescendant(
                    cf => cf.ByAutomationId(automationId));
            if (el != null)
            {
                return el;
            }
            if (!string.IsNullOrEmpty(text))
            {
                var elByText = window
                .FindFirstDescendant(
                    cf => cf.ByText(text));
                if (elByText != null)
                {
                    return elByText;
                }
            }
            return null;
        }, TimeSpan.FromSeconds(10));

        return result;
    }

    /// <summary>金額テキストから数値を抽出して decimal 型で返すヘルパーメソッド。</summary>
    private static decimal ParseAmount(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var cleaned =
            new string([.. text.Where(char.IsDigit)]);
        return decimal
            .TryParse(cleaned, out var result)
            ? result : 0;
    }

    private static void DumpDescendants(AutomationElement element)
    {
        Console.WriteLine($"--- DUMPING DESCENDANTS OF {element.Name} (ID: {element.AutomationId}) ---");
        var descendants = element.FindAllDescendants();
        foreach (var d in descendants)
        {
            Console.WriteLine($"- [{d.ControlType}] Name: '{d.Name}', Id: '{d.AutomationId}', Offscreen: {d.IsOffscreen}");
        }
        Console.WriteLine("--- END DUMP ---");
    }

    public void Dispose()
    {
        _app?.Dispose();
        GC.SuppressFinalize(this);
    }
}
