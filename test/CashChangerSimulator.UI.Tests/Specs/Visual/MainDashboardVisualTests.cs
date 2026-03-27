using CashChangerSimulator.UI.Tests.Helpers;
using FlaUI.Core.Input;

namespace CashChangerSimulator.UI.Tests.Specs.Visual;

[Collection("SequentialTests")]
public class MainDashboardVisualTests
{
    [Fact]
    public async Task VisualRegression_MainDashboard_InitialState()
    {
        using var app = new CashChangerTestApp();
        app.Launch();
        var mainWindow = app.MainWindow;
        
        Assert.NotNull(mainWindow);

        // ウィンドウの初期描画が完全に終わるよう少し待機
        await Task.Delay(1500, TestContext.Current.CancellationToken);
        
        // ゴールデンテストでホバーの影響を受けないようマウスカーソルを退避
        Mouse.Position = new System.Drawing.Point(0, 0);
        
        using var capture = FlaUI.Core.Capturing.Capture.Element(mainWindow);
        using var stream = capture.ToPngStream();

        VerifySettings settings = new();
        settings.UseStrictJson();
        // Explicitly set threshold for this test to 25% to overcome environment rendering differences
        settings.ImageMagickComparer(0.25);
        
        await Verify(stream, "png", settings);
    }
}
