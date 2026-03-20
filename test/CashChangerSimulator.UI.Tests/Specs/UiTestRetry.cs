using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3; 
using System.IO;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>UIオートメーションの再試行とウィンドウ検索を行う静的クラス。</summary>
public static class UiTestRetry
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>条件を満たす要素が見つかるまで、指定されたタイムアウト時間内で再試行を行う。</summary>
    public static AutomationElement? Find(Func<AutomationElement?> func, TimeSpan timeout)
    {
        return Find<AutomationElement>(func, timeout);
    }

    /// <summary>条件を満たす結果が得られるまで、指定されたタイムアウト時間内で再試行を行う（ジェネリック版）。</summary>
    public static T? Find<T>(Func<T?> func, TimeSpan timeout) where T : class
    {
        ArgumentNullException.ThrowIfNull(func);

        T? result = null;
        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            try { result = func(); } catch { }
            return result == null;
        }, timeout);
        return result;
    }

    /// <summary>
    /// アプリケーションからウィンドウを検索し、特定のマーカー要素が存在することを確認して完全に初期化された状態で返します。
    /// </summary>
    public static Window? FindWindow(Application app, UIA3Automation automation, string automationId, TimeSpan? timeout = null, string? markerId = null)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        Window? result = null;

        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            try
            {
                var windows = app.GetAllTopLevelWindows(automation);
                var win = windows.FirstOrDefault(w => w.AutomationId == automationId);
                
                if (win == null) 
                {
                    var desktop = automation.GetDesktop();
                    win = desktop.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsWindow();
                }
                
                if (win != null)
                {
                    if (string.IsNullOrEmpty(markerId))
                    {
                        result = win;
                        return false;
                    }

                    var descendants = win.FindAllDescendants();
                    var marker = descendants.FirstOrDefault(e => e.Properties.AutomationId.ValueOrDefault == markerId);
                    
                    var logContent = $"[{DateTime.Now:HH:mm:ss}] Window {automationId} found. Descendants count: {descendants.Length}. Marker {markerId} found: {marker != null}\n";
                    File.AppendAllText("logs/debug_findwindow.log", logContent);

                    if (marker != null)
                    {
                        result = win;
                        return false;
                    }
                }
                else
                {
                    File.AppendAllText("logs/debug_findwindow.log", $"[{DateTime.Now:HH:mm:ss}] Window {automationId} NOT found.\n");
                }

                return true;
            }
            catch (Exception ex) 
            { 
                File.AppendAllText("logs/debug_findwindow.log", $"[{DateTime.Now:HH:mm:ss}] Exception in FindWindow: {ex.Message}\n");
                return true; 
            }
        }, actualTimeout);

        return result;
    }

    /// <summary>
    /// 要素が有効になるのを安全に待ち、クリック（または Invoke）を実行する。
    /// </summary>
    public static void SmartClick(this AutomationElement? element, int timeoutMs = 2000)
    {
        if (element == null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastException = null;

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (element.IsEnabled)
                {
                    try
                    {
                        var btn = element.AsButton();
                        try { btn.Invoke(); return; }
                        catch { btn.Click(); return; }
                    }
                    catch (Exception ex) { lastException = ex; }
                }
            }
            catch (Exception ex) { lastException = ex; }
            Thread.Sleep(200);
        }

        Console.WriteLine($"SmartClick timeout ({timeoutMs}ms). Last exception: {lastException?.Message}");
        try { element.Click(); } catch { }
    }

    public static void DumpAutomationTree(AutomationElement root, string fileNamePrefix)
    {
        try
        {
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logsDir);
            var path = Path.Combine(logsDir, $"{fileNamePrefix}_{DateTime.Now:HHmmss}.txt");
            
            using var sw = new StreamWriter(path);
            sw.WriteLine($"[DUMP] ROOT: {root.Properties.AutomationId} ({root.Properties.Name}) at {DateTime.Now}");
            sw.WriteLine("--------------------------------------------------------------------------------");
            DumpElementRecursive(root, sw, 0);
            Console.WriteLine($"[DEBUG] UI Tree dumped to: {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] Failed to dump UI Tree: {ex.Message}");
        }
    }

    private static void DumpElementRecursive(AutomationElement element, StreamWriter sw, int indent)
    {
        var prefix = new string(' ', indent * 2);
        try
        {
            var id = element.Properties.AutomationId.ValueOrDefault ?? "N/A";
            var name = element.Properties.Name.ValueOrDefault ?? "N/A";
            var type = element.Properties.ControlType.ValueOrDefault.ToString();
            var isEnabled = element.Properties.IsEnabled.ValueOrDefault;
            var isOffscreen = element.Properties.IsOffscreen.ValueOrDefault;
            
            sw.WriteLine($"{prefix}[{type}] ID:{id} Name:{name} Enabled:{isEnabled} Offscreen:{isOffscreen}");
            sw.Flush(); // Flush each line for safety
            
            foreach (var child in element.FindAllChildren())
            {
                DumpElementRecursive(child, sw, indent + 1);
            }
        }
        catch (Exception ex) { sw.WriteLine($"{prefix}[ACCESS_FAIL: {ex.Message}]"); }
    }
}
