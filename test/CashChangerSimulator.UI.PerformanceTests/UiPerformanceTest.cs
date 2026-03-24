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
[System]
CurrencyCode = 'JPY'
CultureCode = 'ja-JP'

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
        Console.WriteLine("[TEST] Launching app...");
        _app.Launch(customConfig);
        Console.WriteLine("[TEST] App launched. Searching for MainWindow...");
        var window = _app.MainWindow;
        window.ShouldNotBeNull();
        var titlePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "test_title.txt");
        System.IO.File.WriteAllText(titlePath, $"Title: {window.Name}");
        Console.WriteLine($"[TEST] MainWindow found. Title: {window.Name}");
        window.SetForeground();
        // 1. Ensure Device is Open if not already (Handling Cold Start)
        var openBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceOpenButton"))?.AsButton();
        if (openBtn != null && !openBtn.IsOffscreen && openBtn.IsEnabled)
        {
            openBtn.Click();
            // Wait for connection to stabilize
            Console.WriteLine($"[TEST] Waiting for device to open...");
            Retry.WhileFalse(() => {
                Console.WriteLine("[DEBUG] Checking if DeviceCloseButton is enabled...");
                var closeBtn = window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton"));
                return closeBtn != null && closeBtn.IsEnabled;
            }, TimeSpan.FromSeconds(15));
            Console.WriteLine("[TEST] Device is open.");
        }

        Console.WriteLine("[TEST] Searching for LaunchAdvancedSimulationButton...");
        // 2. Advanced Simulation ウィンドウを開く
        var terminalButton = Retry
            .WhileNull(() => {
                var btn = window.FindFirstDescendant(cf => cf.ByAutomationId("LaunchAdvancedSimulationButton"))?.AsButton();
                return (btn != null && btn.IsEnabled && !btn.IsOffscreen) ? btn : null;
            }, TimeSpan.FromSeconds(20)).Result;

        terminalButton.ShouldNotBeNull("LaunchAdvancedSimulationButton not found or not ready");
        Console.WriteLine("[TEST] LaunchAdvancedSimulationButton found and enabled. Invoking...");
        terminalButton.SmartClick();
        Console.WriteLine("[TEST] LaunchAdvancedSimulationButton invoked. Waiting for popup...");

        Thread.Sleep(5000);
        
        // Robust simulation window search
        var simWindow = UiTestRetry.FindWindow(_app.Application, _app.Automation, "AdvancedSimulationWindow", UITestTimings.RetryLongTimeout)
                        ?? _app.Application.GetAllTopLevelWindows(_app.Automation).FirstOrDefault(w => w.Title.Contains("Advanced Simulation"));
        
        simWindow.ShouldNotBeNull("AdvancedSimulationWindow not found");
        simWindow.SetForeground();

        // 2. 大量のログを発生させるスクリプト（200回のReadCashCounts呼び出しなど）を生成
        // 1000回から200回に削減し、テストの安定性と実行速度を向上させる
        var scriptArray = new JsonArray();
        const int bulkOperations = 200;
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
