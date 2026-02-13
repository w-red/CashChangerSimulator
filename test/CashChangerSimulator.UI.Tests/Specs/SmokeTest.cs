using FlaUI.Core.AutomationElements;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

public class SmokeTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    public SmokeTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    [Fact]
    public void ShouldHaveCorrectTitle()
    {
        var window = _app.MainWindow;
        Assert.NotNull(window);
        Assert.Equal("Cash Changer Simulator v1.1.0", window.Title);
    }
    
    [Fact]
    public void TotalAmountShouldBeVisible()
    {
        var totalAmountLabel = _app.MainWindow.FindFirstDescendant(cf => cf.ByText("TOTAL AMOUNT"));
        Assert.NotNull(totalAmountLabel);
    }

    public void Dispose()
    {
        _app.Dispose();
    }
}
