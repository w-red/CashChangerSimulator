using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3; // Added for UIA3Automation type

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>
/// UIオートメーションにおける再試行ロジックと、信頼性の高いウィンドウ検索機能を提供する静的クラス。
/// </summary>
public static class UiTestRetry
{
    /// <summary>
    /// 条件を満たす要素が見つかるまで、指定されたタイムアウト時間内で再試行を行う。
    /// </summary>
    /// <param name="func">要素を返す関数。</param>
    /// <param name="timeout">タイムアウト時間。</param>
    /// <returns>見つかった要素。見つからない場合は null。</returns>
    public static AutomationElement? Find(Func<AutomationElement?> func, TimeSpan timeout)
    {
        AutomationElement? result = null;
        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            result = func();
            return result == null;
        }, timeout);
        return result!;
    }

    /// <summary>
    /// 指定されたアプリケーション、タイトル、または AutomationId に基づいてウィンドウを検索する。
    /// アプリケーションが提供するウィンドウリストで見つからない場合、デスクトップ全体からの検索を試みる。
    /// </summary>
    /// <param name="app">対象のアプリケーションインスタンス。</param>
    /// <param name="automation">オートメーションインスタンス。</param>
    /// <param name="title">ウィンドウのタイトル、または AutomationId。</param>
    /// <param name="timeout">検索のタイムアウト時間。</param>
    /// <returns>見つかったウィンドウ。見つからない場合は null。</returns>
    public static Window? FindWindow(Application app, UIA3Automation automation, string title, TimeSpan timeout)
    {
        Window? result = null;
        var processId = app.ProcessId;
        int counter = 0;
        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            counter++;
            // Try 1: App top level windows and their direct children (WPF Ownership often puts windows as children)
            try
            {
                var topWindows = app.GetAllTopLevelWindows(automation);
                foreach (var w in topWindows)
                {
                    if (w.Title.Contains(title) || (w.Name != null && w.Name.Contains(title)) || w.AutomationId == title)
                    {
                        result = w;
                        return false;
                    }

                    // Look for owned windows which might appear as children in UIA tree
                    var child = w.FindFirstChild(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window).And(
                        cf.ByAutomationId(title).Or(cf.ByName(title))));
                    if (child != null)
                    {
                        result = child.AsWindow();
                        return false;
                    }
                }
            }
            catch { }

            // Try 2: Desktop direct children (Absolute fallback)
            try
            {
                var desktop = automation.GetDesktop();
                var windows = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));

                result = windows.FirstOrDefault(w =>
                    (w.Properties.ProcessId == processId || true) && // Relax PID for fallback
                    (w.AutomationId == title || (w.Name != null && w.Name.Contains(title))))?.AsWindow();
            }
            catch { }

            return result == null;
        }, timeout);

        if (result == null)
        {
            // Debug: List all windows if not found (will show in test output if assertions follow)
            Console.WriteLine($"Failed to find window '{title}' for PID {processId}. Available windows for this PID:");
            try
            {
                var desktop = automation.GetDesktop();
                var all = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                foreach (var w in all.Where(x => x.Properties.ProcessId == processId))
                {
                    Console.WriteLine($"- Name: '{w.Name}', Id: '{w.AutomationId}'");
                }
            }
            catch { }
        }

        return result!;
    }
}
