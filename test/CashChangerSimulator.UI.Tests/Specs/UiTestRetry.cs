using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3; // Added for UIA3Automation type

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>UIオートメーションの再試行とウィンドウ検索を行う静的クラス。</summary>
public static class UiTestRetry
{
    /// <summary>条件を満たす要素が見つかるまで、指定されたタイムアウト時間内で再試行を行う。</summary>
    /// <param name="func">要素を返す関数。</param>
    /// <param name="timeout">タイムアウト時間。</param>
    /// <returns>見つかった要素。見つからない場合は null。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> が null の場合にスローされます。</exception>
    public static AutomationElement? Find(Func<AutomationElement?> func, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(func);

        AutomationElement? result = null;
        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            result = func();
            return result == null;
        }, timeout);
        return result;
    }

    /// <summary>アプリケーション、タイトル、または AutomationId に基づいてウィンドウを検索する。</summary>
    /// <param name="app">対象のアプリケーションインスタンス。</param>
    /// <param name="automation">オートメーションインスタンス。</param>
    /// <param name="title">ウィンドウのタイトル、または AutomationId。</param>
    /// <param name="timeout">検索のタイムアウト時間。</param>
    /// <returns>見つかったウィンドウ。見つからない場合は null。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/>, <paramref name="automation"/>, または <paramref name="title"/> が null の場合にスローされます。</exception>
    public static Window? FindWindow(Application app, UIA3Automation automation, string title, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(automation);
        ArgumentNullException.ThrowIfNull(title);

        Window? result = null;
        var processId = app.ProcessId;
        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            // Try 1: App top level windows (Fastest)
            try
            {
                var topWindows = app.GetAllTopLevelWindows(automation);
                foreach (var w in topWindows)
                {
                    var id = w.AutomationId;
                    var name = w.Name;
                    var titleProp = w.Title;
                    if (id == title || (name?.Contains(title, StringComparison.OrdinalIgnoreCase) ?? false) || (titleProp?.Contains(title, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        result = w;
                        return false;
                    }
                }
            }
            catch { }

            // Try 2: Desktop children filtered by ProcessId (Most Reliable for spawned windows)
            try
            {
                var desktop = automation.GetDesktop();
                var processWindows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window))
                                            .Where(w => w.Properties.ProcessId == processId)
                                            .ToList();

                foreach (var w in processWindows)
                {
                    var id = w.AutomationId;
                    var name = w.Name;
                    if (id == title || (name?.Contains(title, StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        result = w.AsWindow();
                        return false;
                    }
                }
            }
            catch { }

            return result == null;
        }, timeout);

        if (result == null)
        {
            Console.WriteLine($"[ERROR] Window '{title}' NOT FOUND for ProcessId {processId} after {timeout.TotalSeconds}s.");
            Console.WriteLine("Dumping ALL windows on Desktop for context:");
            try
            {
                var desktop = automation.GetDesktop();
                var all = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                foreach (var w in all)
                {
                    Console.WriteLine($"- Window: Name='{w.Name ?? "(null)"}', ID='{w.AutomationId ?? "(null)"}', PID={w.Properties.ProcessId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Failed to dump windows: {ex.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// 要素が有効になるのを安全に待ち、クリック（または Invoke）を実行する。
    /// COMException や ElementNotEnabledException などを補足しリトライする。
    /// 無限の待機によるテストのタイムアウトを防ぐため、一定回数でフォールバックを行う。
    /// </summary>
    /// <param name="element">クリック対象のUI要素。</param>
    /// <param name="timeoutMs">最大待機時間（ミリ秒） デフォルトは2000ms。</param>
    public static void SmartClick(this AutomationElement? element, int timeoutMs = 2000)
    {
        if (element == null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastException = null;

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                // IsEnabledの再取得はCOM例外を誘発しやすいためtry-catch内で判定
                if (element.IsEnabled)
                {
                    try
                    {
                        var btn = element.AsButton();
                        try
                        {
                            btn.Invoke(); // パターンがサポートされていれば優先
                            return;
                        }
                        catch
                        {
                            btn.Click(); // フォールバック
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
            Thread.Sleep(200);
        }

        // タイムアウトした場合は最後に記録された例外とともに強制クリックを試みる
        Console.WriteLine($"SmartClick timeout ({timeoutMs}ms). Last exception: {lastException?.Message}");
        element.Click();
    }
}
