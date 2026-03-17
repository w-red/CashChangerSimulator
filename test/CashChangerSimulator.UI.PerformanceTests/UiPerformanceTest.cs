using CashChangerSimulator.UI.Tests;
using CashChangerSimulator.UI.Tests.Specs;
using FlaUI.Core.AutomationElements;
using Shouldly;
using System.Diagnostics;
using System.Text.Json.Nodes;
using FlaUI.Core.Tools;

namespace CashChangerSimulator.UI.PerformanceTests;

/// <summary>
/// ZLogger等の連携時に、大量のトランザクションがUIの応答性に深刻な影響を与えないかを検証するUIパフォーマンステスト。
/// </summary>
[Collection("SequentialTests")]
public class UiPerformanceTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    public UiPerformanceTest()
    {
        _app = new CashChangerTestApp();
    }

    /// <summary>
    /// 高度なシミュレーションウィンドウから大量のスクリプトコマンドを実行し、
    /// ZLogger の大量出力中であってもUIがフリーズせずに応答し続けるかをテストします。
    /// </summary>
    [Fact]
    public void ShouldKeepUiResponsiveDuringHeavyLoggingLoad()
    {
        // 高速で実行させるため、ハードウェア遅延をシミュレーション設定でゼロに近づける
        var customConfig = """
[Thresholds]
NearEmpty = 10
NearFull = 90
Full = 100

[Inventory.JPY.Denominations.C100]
InitialCount = 50
NearEmpty = 10
NearFull = 90
Full = 100

[Simulation]
DispenseDelayMs = 0
HotStart = true
""";
        _app.Launch(customConfig);
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        window.SetForeground();
        // 1. Advanced Simulation ウィンドウを開く
        var terminalButton = Retry.WhileNull(() => {
            var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchAdvancedSimulationButton"))?.AsButton();
            return (btn != null && btn.IsEnabled && !btn.IsOffscreen) ? btn : null;
        }, TimeSpan.FromSeconds(20)).Result;

        terminalButton.ShouldNotBeNull("LaunchAdvancedSimulationButton not found or not ready");
        if (terminalButton.Patterns.Invoke.IsSupported)
        {
            terminalButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            terminalButton.Click();
        }

        Thread.Sleep(UITestTimings.WindowPopupDelayMs);
        var simWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "Advanced Simulation Controls", UITestTimings.RetryLongTimeout);
        simWindow.ShouldNotBeNull();
        simWindow.SetForeground();

        // 2. 大量のログを発生させるスクリプト（1000回のReadCashCounts呼び出しなど）を生成
        var scriptArray = new JsonArray();
        const int bulkOperations = 1000;
        for (int i = 0; i < bulkOperations; i++)
        {
            scriptArray.Add(new JsonObject { ["Op"] = "ReadCashCounts" });
        }
        string scriptJson = scriptArray.ToJsonString();

        // 3. スクリプトを入力
        var scriptBox = UiTestRetry.Find(() => simWindow.FindFirstDescendant(cf => cf.ByAutomationId("ScriptInputTextBox"))?.AsTextBox(), UITestTimings.RetryLongTimeout) as TextBox;
        scriptBox.ShouldNotBeNull();
        scriptBox.Text = scriptJson;

        // 4. スクリプトの実行と応答性の計測
        var execButton = UiTestRetry.Find(() => simWindow.FindFirstDescendant(cf => cf.ByAutomationId("ExecuteScriptButton"))?.AsButton(), UITestTimings.RetryLongTimeout) as Button;
        execButton.ShouldNotBeNull();

        var sw = Stopwatch.StartNew();
        if (execButton.Patterns.Invoke.IsSupported)
        {
            execButton.Patterns.Invoke.Pattern.Invoke();
        }
        else
        {
            execButton.Click();
        }

        // --- 応答性検証 ---
        // 実行直後に何度もメインウィンドウへのアクセスを試みることで、UIスレッドがロックされていないか確認する。
        bool UIThreadBlocked = false;
        long maxResponseTimeMs = 0;

        // 5秒間、定期的にメインウィンドウのプロパティ（Title等）を読み取る
        var checkEnd = DateTime.Now.AddSeconds(5);
        while (DateTime.Now < checkEnd)
        {
            var checkSw = Stopwatch.StartNew();
            try
            {
                // UIツリーへのアクセス（RPC呼び出し）を試みる
                var title = window.Name;
                checkSw.Stop();

                if (checkSw.ElapsedMilliseconds > maxResponseTimeMs)
                {
                    maxResponseTimeMs = checkSw.ElapsedMilliseconds;
                }

                // もしプロパティ取得に極端な遅延（例：500ms以上）があればフリーズと見なす
                if (checkSw.ElapsedMilliseconds > 1000)
                {
                    UIThreadBlocked = true;
                }
            }
            catch
            {
                // エラーはプロセス終了か応答なし扱い
                UIThreadBlocked = true;
            }

            Thread.Sleep(50);
        }

        sw.Stop();

        // アサーション：極端なUIスレッドブロックが発生していないこと
        UIThreadBlocked.ShouldBeFalse($"UI Thread was blocked significantly during heavy load. Max response time: {maxResponseTimeMs}ms");
        Console.WriteLine($"UI Performance Test Passed. Max UI response latency during {bulkOperations} operations was {maxResponseTimeMs}ms.");
    }

    public void Dispose()
    {
        _app?.Dispose();
        GC.SuppressFinalize(this);
    }
}
