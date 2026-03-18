using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>アプリケーションの起動と基本表示を検証するスモークテスト。</summary>
public class SmokeTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリを起動して初期化する。</summary>
    public SmokeTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    /// <summary>ウィンドウのタイトルが正しいことを検証する。</summary>
    [Fact]
    public void ShouldHaveCorrectTitle()
    {
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        // Allow both English and Japanese titles
        window.Title.ShouldBeOneOf("Cash Changer Simulator", "自動釣銭機シミュレーター");
    }

    /// <summary>合計金額のラベルが表示されていることを検証する。</summary>
    [Fact]
    public void TotalAmountShouldBeVisible()
    {
        // Use AutomationId instead of localized text
        var totalAmountLabel = _app.MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("CashBalanceLabel"));
        totalAmountLabel.ShouldNotBeNull();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _app.Dispose();
        GC.SuppressFinalize(this);
    }
}
