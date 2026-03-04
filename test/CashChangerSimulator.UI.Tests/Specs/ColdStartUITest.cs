using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
using Xunit;
using System;
using System.Threading;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>コールドスタート（HotStart=false）時のUI動作を検証するテストクラス。</summary>
[Collection("SequentialTests")]
public class ColdStartUITest : IDisposable
{
    private readonly CashChangerTestApp _app;

    public ColdStartUITest()
    {
        _app = new CashChangerTestApp();
    }

    /// <summary>HotStart=false で起動した際に、UIが適切に制限されていることを検証する。</summary>
    [Fact]
    public void ShouldInitializeWithRestrictedUIWhenHotStartIsDisabled()
    {
        // Arrange & Act
        _app.Launch(hotStart: false);
        var window = _app.MainWindow;
        window.ShouldNotBeNull();

        // Assert - Terminal Access buttons should be disabled
        var depositButton = FindElement(window, "LaunchDepositButton")?.AsButton();
        var dispenseButton = FindElement(window, "LaunchDispenseButton")?.AsButton();
        var advancedButton = FindElement(window, "LaunchAdvancedSimulationButton")?.AsButton();

        depositButton.ShouldNotBeNull();
        dispenseButton.ShouldNotBeNull();
        advancedButton.ShouldNotBeNull();

        depositButton.IsEnabled.ShouldBeFalse();
        dispenseButton.IsEnabled.ShouldBeFalse();
        advancedButton.IsEnabled.ShouldBeFalse();

        // Assert - Open button should be visible, Close button should not be found (or hidden)
        var openButton = FindElement(window, "DeviceOpenButton")?.AsButton();
        openButton.ShouldNotBeNull();
        openButton.IsOffscreen.ShouldBeFalse();

        var closeButton = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton"));
        (closeButton == null || closeButton.IsOffscreen).ShouldBeTrue();

        // Assert - Mode Indicator should show CLOSED
        var modeIndicator = FindElement(window, "ModeIndicatorText")?.AsLabel();
        modeIndicator.ShouldNotBeNull();
        modeIndicator.Text.ShouldContain("CLOSED");
    }

    /// <summary>オープン操作によってUIが有効化されることを検証する。</summary>
    [Fact]
    public void ShouldEnableUIFollowingOpenOperation()
    {
        // Arrange
        _app.Launch(hotStart: false);
        var window = _app.MainWindow;
        window.ShouldNotBeNull();

        // Act - Click Open
        var openButton = FindElement(window, "DeviceOpenButton")?.AsButton();
        openButton.ShouldNotBeNull();
        openButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Assert - Terminal buttons should become enabled
        var depositButton = FindElement(window, "LaunchDepositButton")?.AsButton();
        var dispenseButton = FindElement(window, "LaunchDispenseButton")?.AsButton();
        
        depositButton.ShouldNotBeNull();
        dispenseButton.ShouldNotBeNull();

        Retry.WhileFalse(() => depositButton.IsEnabled, UITestTimings.RetryLongTimeout);
        dispenseButton.IsEnabled.ShouldBeTrue();

        // Assert - Open button should be hidden, Close button should be visible
        var openBtnAfter = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceOpenButton"));
        (openBtnAfter == null || openBtnAfter.IsOffscreen).ShouldBeTrue();

        var closeButton = FindElement(window, "DeviceCloseButton")?.AsButton();
        closeButton.ShouldNotBeNull();
        closeButton.IsOffscreen.ShouldBeFalse();
    }

    /// <summary>クローズ操作によってUIが再び制限されることを検証する。</summary>
    [Fact]
    public void ShouldRestrictUIFollowingCloseOperation()
    {
        // Arrange
        _app.Launch(hotStart: false);
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        
        // Open it first
        var openButton = FindElement(window, "DeviceOpenButton")?.AsButton();
        openButton?.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Act - Click Close
        var closeButton = FindElement(window, "DeviceCloseButton")?.AsButton();
        closeButton.ShouldNotBeNull();
        closeButton.Click();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Assert - UI should be restricted again
        var depositButton = FindElement(window, "LaunchDepositButton")?.AsButton();
        depositButton.ShouldNotBeNull();
        Retry.WhileTrue(() => depositButton.IsEnabled, UITestTimings.RetryLongTimeout);

        var openBtnAgain = FindElement(window, "DeviceOpenButton")?.AsButton();
        openBtnAgain.ShouldNotBeNull();
        openBtnAgain.IsOffscreen.ShouldBeFalse();
    }

    private static FlaUI.Core.AutomationElements.AutomationElement? FindElement(FlaUI.Core.AutomationElements.AutomationElement? container, string automationId)
    {
        if (container == null) return null;
        return UiTestRetry.Find(() => container.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), UITestTimings.RetryLongTimeout);
    }

    public void Dispose()
    {
        _app.Dispose();
        GC.SuppressFinalize(this);
    }
}
