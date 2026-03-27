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

    /// <summary>アプリケーションからウィンドウを検索し、初期化が完了した状態で返す。</summary>
    /// <param name="app">アプリケーションインスタンス。</param>
    /// <param name="automation">オートメーションインスタンス。</param>
    /// <param name="automationId">検索するウィンドウのオートメーションID。</param>
    /// <param name="timeout">タイムアウト時間。</param>
    /// <param name="markerId">初期化完了を確認するためのマーカー要素のID。</param>
    /// <returns>見つかったウィンドウ、または null。</returns>
    public static Window? FindWindow(Application app, UIA3Automation automation, string automationId, TimeSpan? timeout = null, string? markerId = null)
    {
        var actualTimeout = timeout ?? DefaultTimeout;
        Window? result = null;

        FlaUI.Core.Tools.Retry.WhileTrue(() =>
        {
            try
            {
                var windows = app.GetAllTopLevelWindows(automation);
                var allWinIds = string.Join(", ", windows.Select(w => $"{w.Properties.AutomationId.ValueOrDefault ?? "NoID"}({w.Title})"));
                File.AppendAllText("logs/debug_findwindow.log", $"[{DateTime.Now:HH:mm:ss}] Searching for {automationId}. Found App Windows: [{allWinIds}]\n");
                
                var win = windows.FirstOrDefault(w => w.Properties.AutomationId.ValueOrDefault == automationId);
                
                if (win == null) 
                {
                    var desktop = automation.GetDesktop();
                    var allDesktopChildren = desktop.FindAllChildren(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window));
                    var desktopIds = string.Join(", ", allDesktopChildren.Select(w => $"{w.Properties.AutomationId.ValueOrDefault ?? "NoID"}({w.Name})"));
                    File.AppendAllText("logs/debug_findwindow.log", $"[{DateTime.Now:HH:mm:ss}] Searching for {automationId} in Desktop: [{desktopIds}]\n");
                    
                    win = allDesktopChildren
                        .FirstOrDefault(w => w.Properties.AutomationId.ValueOrDefault == automationId)
                        ?.AsWindow();
                }

                if (win == null) 
                {
                    // Last resort: deep search from desktop
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

                    // Wait for marker (initialization indicator) with short sleeps
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var markerTimeout = TimeSpan.FromSeconds(5);
                    while (sw.Elapsed < markerTimeout)
                    {
                        try
                        {
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
                        catch { }
                        Thread.Sleep(300);
                    }

                    // Marker not found within 5s but window exists: return window anyway to avoid full timeout
                    File.AppendAllText("logs/debug_findwindow.log", $"[{DateTime.Now:HH:mm:ss}] Marker {markerId} not found within 5s but window exists. Returning window.\n");
                    result = win;
                    return false;
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

    /// <summary>要素が有効になるのを待ち、最適な方法（Invoke または Click）でクリックを実行する。</summary>
    /// <param name="element">対象のオートメーション要素。</param>
    /// <param name="timeoutMs">タイムアウト（ミリ秒）。</param>
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
                    // 1. Try Invoke Pattern (Most reliable in CI)
                    if (element.Patterns.Invoke.IsSupported)
                    {
                        try { element.Patterns.Invoke.Pattern.Invoke(); return; }
                        catch (Exception ex) { lastException = ex; }
                    }

                    // 2. Try Click WITHOUT mouse move (Supported by some elements)
                    try 
                    { 
                        element.Click(false); 
                        return; 
                    }
                    catch (Exception ex) { lastException = ex; }

                    // 3. Try standard Click (May fail in CI with "Access is denied" if it tries to move mouse)
                    try
                    {
                        element.Click(true);
                        return;
                    }
                    catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // Access is denied
                    {
                        lastException = ex;
                        // If standard click fails due to mouse move restriction, 
                        // and invoke wasn't supported, we are in trouble, but let's try one last thing: Focus + Enter
                        try { element.Focus(); FlaUI.Core.Input.Keyboard.Type(FlaUI.Core.WindowsAPI.VirtualKeyShort.RETURN); return; }
                        catch { }
                    }
                    catch (Exception ex) { lastException = ex; }
                }
            }
            catch (Exception ex) { lastException = ex; }
            Thread.Sleep(200);
        }

        Console.WriteLine($"SmartClick timeout ({timeoutMs}ms). Last exception: {lastException?.Message}");
    }

    /// <summary>指定された要素以下のオートメーションツリーをファイルに出力する。</summary>
    /// <param name="root">ルート要素。</param>
    /// <param name="fileNamePrefix">ファイル名のプレフィックス。</param>
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

    /// <summary>オートメーション要素を再帰的に探索し、詳細情報をストリームに書き込む。</summary>
    /// <param name="element">現在の要素。</param>
    /// <param name="sw">書き込み先のストリーム。</param>
    /// <param name="indent">インデントレベル。</param>
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
