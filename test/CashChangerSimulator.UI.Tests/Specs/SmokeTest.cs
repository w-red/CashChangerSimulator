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
        window.Title.ShouldBe("Cash Changer Simulator");
    }

    /// <summary>合計金額のラベルが表示されていることを検証する。</summary>
    [Fact]
    public void TotalAmountShouldBeVisible()
    {
        var totalAmountLabel = _app.MainWindow!.FindFirstDescendant(cf => cf.ByText("CASH BALANCE"));
        totalAmountLabel.ShouldNotBeNull();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _app.Dispose();
        GC.SuppressFinalize(this);
    }
}
