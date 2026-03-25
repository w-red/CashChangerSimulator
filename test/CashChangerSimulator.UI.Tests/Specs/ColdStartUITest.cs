using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>コールドスタート（HotStart=false）時のUI動作を検証するテストクラス。</summary>
[Collection("SequentialTests")]
public class ColdStartUITest : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリのフィクスチャを受け取り、初期状態をセットアップする。</summary>
    public ColdStartUITest(CashChangerTestApp app)
    {
        _app = app;
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
        openButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Assert - Terminal buttons should become enabled
        var depositButton = FindElement(window, "LaunchDepositButton")?.AsButton();
        var dispenseButton = FindElement(window, "LaunchDispenseButton")?.AsButton();

        depositButton.ShouldNotBeNull();
        dispenseButton.ShouldNotBeNull();

        Retry.WhileFalse(() => depositButton.IsEnabled, UITestTimings.RetryLongTimeout);
        Retry.WhileFalse(() => dispenseButton.IsEnabled, UITestTimings.RetryLongTimeout);

        // Final re-fetch and check
        // Final re-fetch and check
        AutomationElement? finalDispenseButton = null;
        Retry.WhileTrue(() =>
        {
            try
            {
                finalDispenseButton = FindElement(window, "LaunchDispenseButton");
                return finalDispenseButton == null || !finalDispenseButton.AsButton().IsEnabled;
            }
            catch { return true; } // Ignore COM exceptions during property retrieval
        }, UITestTimings.RetryLongTimeout);

        finalDispenseButton.ShouldNotBeNull();
        finalDispenseButton.AsButton().IsEnabled.ShouldBeTrue();

        // Assert - Open button should be hidden, Close button should be visible
        var openBtnAfter = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceOpenButton"));
        (openBtnAfter == null || openBtnAfter.IsOffscreen).ShouldBeTrue();

        AutomationElement? closeButton = null;
        Retry.WhileTrue(() =>
        {
            try
            {
                closeButton = FindElement(window, "DeviceCloseButton");
                return closeButton == null || closeButton.AsButton().IsOffscreen;
            }
            catch { return true; }
        }, UITestTimings.RetryLongTimeout);

        closeButton.ShouldNotBeNull();
        closeButton.AsButton().IsOffscreen.ShouldBeFalse();
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
        openButton?.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Act - Click Close
        var closeButton = FindElement(window, "DeviceCloseButton")?.AsButton();
        closeButton.ShouldNotBeNull();
        closeButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Assert - UI should be restricted again
        // Assert - UI should be restricted again
        AutomationElement? finalDepositButton = null;
        Retry.WhileTrue(() =>
        {
            try
            {
                finalDepositButton = FindElement(window, "LaunchDepositButton");
                // Here we want it to be disabled (IsEnabled == false), so we retry while it is true
                return finalDepositButton == null || finalDepositButton.AsButton().IsEnabled;
            }
            catch { return true; }
        }, UITestTimings.RetryLongTimeout);

        finalDepositButton.ShouldNotBeNull();
        finalDepositButton.AsButton().IsEnabled.ShouldBeFalse();

        var openBtnAgain = FindElement(window, "DeviceOpenButton")?.AsButton();
        openBtnAgain.ShouldNotBeNull();
        openBtnAgain.IsOffscreen.ShouldBeFalse();
    }

    /// <summary>指定されたオートメーションIDを持つ要素を探索する。</summary>
    /// <param name="container">親要素。</param>
    /// <param name="automationId">探索するオートメーションID。</param>
    /// <returns>見つかった要素、または null。</returns>
    private static AutomationElement? FindElement(AutomationElement? container, string automationId)
    {
        return container == null
            ? null
            : UiTestRetry.Find(() => container.FindFirstDescendant(cf => cf.ByAutomationId(automationId)), UITestTimings.RetryLongTimeout);
    }

}
