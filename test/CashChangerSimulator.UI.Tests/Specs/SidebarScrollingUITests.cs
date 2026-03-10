using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

public class SidebarScrollingUITests : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    public SidebarScrollingUITests(CashChangerTestApp app)
    {
        _app = app;
    }

    [Fact]
    public void TerminalAccessShouldStayVisibleWhenHistoryGrows()
    {
        // Arrange
        _app.Launch(hotStart: true);
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");

        // Force a smaller window size to ensure scrolling occurs with 50 entries
        window.Move(0, 0);
        if (window.Patterns.Transform.IsSupported)
        {
            window.Patterns.Transform.Pattern.Resize(1240, 600);
        }

        // Open the device to enable terminal buttons
        var openBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceOpenButton"))?.AsButton();
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            openBtn.Click();
        }

        // Wait for Idle state
        Retry.WhileFalse(() => {
            var modeText = window.FindFirstDescendant(cf => cf.ByAutomationId("ModeIndicatorText"))?.AsLabel();
            return modeText != null && (modeText.Text == "IDLE" || modeText.Text == "待機中");
        }, TimeSpan.FromSeconds(20)).Success.ShouldBeTrue("Device did not enter IDLE state");

        var depositBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDepositButton"))?.AsButton();
        depositBtn.ShouldNotBeNull("LaunchDepositButton not found");
        
        // Record initial position
        var initialY = depositBtn.BoundingRectangle.Y;

        // Act: Generate many transactions to trigger scrolling in the sidebar
        var closeBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton"))?.AsButton();
        
        for (int i = 0; i < 50; i++)
        {
            closeBtn.Click();
            Thread.Sleep(200);
            openBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceOpenButton"))?.AsButton(), TimeSpan.FromSeconds(5)).Result;
            openBtn.Click();
            Thread.Sleep(200);
            closeBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton"))?.AsButton(), TimeSpan.FromSeconds(5)).Result;
        }

        // Wait for layout update
        Thread.Sleep(1000);

        // Act: Scroll the inner ScrollViewer to the bottom
        var sidebarScrollViewer = window.FindFirstDescendant(cf => cf.ByAutomationId("ActivityFeedScrollViewer"));
        sidebarScrollViewer.ShouldNotBeNull("ActivityFeed ScrollViewer not found");
        if (sidebarScrollViewer.Patterns.Scroll.IsSupported)
        {
            // Scroll down as much as possible
            sidebarScrollViewer.Patterns.Scroll.Pattern.SetScrollPercent(-1, 100);
        }
        Thread.Sleep(500);

        // Assert: LaunchDepositButton should stay in the SAME position
        // In the buggy implementation, it will have moved UP (smaller Y) because it's inside the scrolled container.
        depositBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchDepositButton"))?.AsButton();
        depositBtn.ShouldNotBeNull();
        
        var finalY = depositBtn.BoundingRectangle.Y;
        
        // Expected to fail here in the Red phase
        finalY.ShouldBe(initialY, $"LaunchDepositButton moved from {initialY} to {finalY}! It should stay at a fixed position.");
    }
}
