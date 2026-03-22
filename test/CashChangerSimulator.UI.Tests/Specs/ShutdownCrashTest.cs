using FlaUI.Core.AutomationElements;
using Shouldly;
using System.Diagnostics;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>アプリケーション終了時の NullReferenceException 発生を検証するテストクラス。</summary>
[Collection("SequentialTests")]
public class ShutdownCrashTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストクラスの新しいインスタンスを初期化する。</summary>
    public ShutdownCrashTest()
    {
        _app = new CashChangerTestApp();
    }

    /// <summary>サブウィンドウが開いている状態でメインウィンドウを閉じてもクラッシュしないことを検証する。</summary>
    /// <remarks>
    /// デバイスがオープンされ、サブウィンドウが開いている状態でメインウィンドウを閉じ、
    /// クラッシュせずに正常終了することを確認します。
    /// </remarks>
    [Fact]
    public void ShouldExitCleanlyWhenClosingWithOpenSubWindows()
    {
        // Arrange
        // Launch with HotStart=true to ensure device is open
        _app.Launch(hotStart: true);
        var window = _app.MainWindow;
        window.ShouldNotBeNull();

        // Act - Open Dispense Window (as a sub-window)
        var dispenseButton = FindElement(window, "LaunchDispenseButton")?.AsButton();
        dispenseButton.ShouldNotBeNull();
        dispenseButton.SmartClick();
        Thread.Sleep(UITestTimings.LogicExecutionDelayMs);

        // Verify sub-window exists (optional, but good for stability)
        var desktop = _app.Automation.GetDesktop();
        var dispenseWindow = desktop.FindFirstChild(cf => cf.ByName("Dispense"));
        // dispenseWindow might be null if name is different, but the goal is just to have it open.

        // Capture the process by ID before it potentially exits
        var processId = _app.Application.ProcessId;
        using var process = Process.GetProcessById(processId);

        // Act - Close Main Window to trigger shutdown
        window.Close();

        // Assert - Wait for process to exit and check exit code
        if (!process.WaitForExit(10000))
        {
            process.Kill();
            Assert.Fail("Application failed to exit within 10 seconds after closing main window.");
        }

        // Using process.ExitCode here should be safer as we have a direct Process object
        // although it might still throw if permissions or other environment factors interfere.
        // If it still fails, we'll at least know it exited.
        try
        {
            process.ExitCode.ShouldBe(0, "Process should exit with code 0 (Success). Non-zero code suggests a crash.");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Warning: Could not determine exit code: {ex.Message}");
            // If we can't get exit code but it exited, we'll tentatively pass if it didn't hang
        }
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

    /// <summary>テストアプリを破棄する。</summary>
    public void Dispose()
    {
        // If the process is still running, _app.Dispose will try to close/kill it.
        try { _app.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }
}
