using CashChangerSimulator.UI.Tests.Helpers;
using CashChangerSimulator.UI.Tests.Specs;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System.IO;
using System.Reflection;

namespace CashChangerSimulator.UI.Tests;

/// <summary>UI テスト用のヘルパークラス。App インスタンスの管理や画面操作の抽象化を担当する。</summary>
public class CashChangerTestApp : IDisposable
{
    /// <summary>テスト対象のアプリケーションインスタンス。</summary>
    public Application Application { get; private set; } = null!;

    /// <summary>UI Automation オートメーションクラス。</summary>
    public UIA3Automation Automation { get; private set; } = null!;

    /// <summary>アプリケーションのメインウィンドウ。</summary>
    public Window? MainWindow { get; private set; }
    private readonly string _executablePath;

    /// <summary>クラスの新しいインスタンスを初期化し、実行ファイルのパスを特定する。</summary>
    public CashChangerTestApp()
    {
        // Calculate path to the executable relative to the test assembly
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyLocation);
        if (string.IsNullOrEmpty(assemblyDir)) throw new Exception("Could not determine assembly directory.");

        // Determine configuration based on the test assembly build
        string config = "Debug";
#if !DEBUG
        config = "Release";
#endif

        // [DEFINITIVE FORCE] Use the publish folder if it exists to ensure we aren't using stale cached binaries.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var solutionRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        var publishPath = Path.Combine(solutionRoot, "test_publish", "CashChangerSimulator.UI.Wpf.exe");
        var srcExePath = Path.Combine(solutionRoot, "src", "CashChangerSimulator.UI.Wpf", "bin", config, "net10.0-windows", "CashChangerSimulator.UI.Wpf.exe");
        
        var exePath = File.Exists(publishPath) ? publishPath : (File.Exists(srcExePath) ? srcExePath : Path.Combine(baseDir, "CashChangerSimulator.UI.Wpf.exe"));
        _executablePath = exePath;
        var fullPath = Path.GetFullPath(exePath);

        if (!File.Exists(_executablePath))
        {
            // Fallback or throw
            throw new FileNotFoundException($"Application executable not found. Tried: {_executablePath}. Ensure the application is built.");
        }
    }

    /// <summary>テスト対象のアプリケーションを起動し、初期化を行う。</summary>
    /// <param name="customConfigToml">カスタム設定の TOML 文字列。</param>
    /// <param name="hotStart">デバイスをオープン状態で開始するかどうか。</param>
    /// <param name="envVars">追加の環境変数。</param>
    public void Launch(string? customConfigToml = null, bool hotStart = true, Dictionary<string, string>? envVars = null)
    {
        // [STABILITY] Ensure previous session is cleaned up
        Dispose();
        KillZombieProcesses();

        try
        {
            string fullPath = Path.GetFullPath(_executablePath);
            string? appDir = Path.GetDirectoryName(fullPath);

            // Clean up state files to ensure a fresh start for each test
            if (appDir != null)
            {
                var filesToClean = new[] { "inventory.toml", "history.bin", "config.toml" };
                foreach (var file in filesToClean)
                {
                    var filePath = Path.Combine(appDir, file);
                    try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
                }

                // Write custom config or default with hotStart properly set
                if (!string.IsNullOrEmpty(customConfigToml))
                {
                    var configPath = Path.Combine(appDir, "config.toml");
                    File.WriteAllText(configPath, customConfigToml);
                }
                else
                {
                    // Create a basic config that enforces the desired HotStart state for the test
                    var configPath = Path.Combine(appDir, "config.toml");
                    var configContent = $@"[System]
CurrencyCode = 'JPY'
CultureCode = 'ja-JP'

[Simulation]
DispenseDelayMs = 500
HotStart = {hotStart.ToString().ToLower()}

[Inventory.JPY.Denominations]
B10000 = {{ InitialCount = 10, DisplayNameJP = '一万円札' }}
B5000  = {{ InitialCount = 10, DisplayNameJP = '五千円札' }}
B1000  = {{ InitialCount = 50, DisplayNameJP = '千円札' }}
C500   = {{ InitialCount = 50, DisplayNameJP = '五百円玉' }}
C100   = {{ InitialCount = 100, DisplayNameJP = '百円玉' }}
C50    = {{ InitialCount = 100, DisplayNameJP = '五十円玉' }}
C10    = {{ InitialCount = 100, DisplayNameJP = '十円玉' }}
C5     = {{ InitialCount = 100, DisplayNameJP = '五円玉' }}
C1     = {{ InitialCount = 100, DisplayNameJP = '一円玉' }}
";
                    File.WriteAllText(configPath, configContent);
                }
            }

            Console.WriteLine($"[CashChangerTestApp] Launching: {fullPath}");

            // Start fresh
            Automation = new UIA3Automation();
            var startInfo = new System.Diagnostics.ProcessStartInfo(_executablePath);
            startInfo.EnvironmentVariables["SKIP_STATE_VERIFICATION"] = "true";
            if (envVars != null)
            {
                foreach (var kvp in envVars)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }
            Application = Application.Launch(startInfo);
            
            // [STABILITY] Wait for process and UI to settle
            Thread.Sleep(3000);

            // Use a more robust wait for the window
            var isCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            var windowWaitTimeout = isCi ? TimeSpan.FromSeconds(45) : TimeSpan.FromSeconds(20);

            Console.WriteLine($"[TEST] Starting MainWindow search (Timeout: {windowWaitTimeout.TotalSeconds}s, PID: {Application.ProcessId})...");
            MainWindow = SearchMainWindow(windowWaitTimeout);

            if (MainWindow == null)
            {
                Console.WriteLine("[ERROR] Main window not found after retry.");
                // Dump ALL windows for debugging if it fails
                try
                {
                    var all = Automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                    foreach (var w in all) Console.WriteLine($"- Global Window: Name='{w.Name}', ID='{w.AutomationId}', PID={w.Properties.ProcessId}");
                } catch { }

                if (isCi)
                {
                    Console.WriteLine("[WARNING] Proceeding in CI even if MainWindow is null to see if subsequent steps can recover or provide more logs.");
                }
                else
                {
                    throw new Exception("Main window 'Cash Changer Simulator' (or ID 'MainWindow') not found.");
                }
            }

            // Settlement period for UI Automation state
            System.Threading.Thread.Sleep(500);

#if !GITHUB_ACTIONS
            if (MainWindow != null)
            {
                try { MainWindow.SetForeground(); } catch { }
                try { MainWindow.WaitUntilClickable(TimeSpan.FromSeconds(5)); } catch { }
            }
#endif
            else
            {
                Console.WriteLine("[INFO] Skipping WaitUntilClickable in CI environment or if MainWindow is null.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FATAL] Launch failed: {ex.Message}");
            // 起動中に失敗した場合、中途半端に起動したプロセスを掃除してから例外を投げ直す
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// メインウィンドウを検索・取得します。既に取得済みの場合はそれを返しますが、見つからない場合は再検索を試みます。
    /// </summary>
    /// <param name="timeout">検索タイムアウト。省略時はデフォルト値を使用します。</param>
    /// <returns>見つかったウィンドウ、または null。</returns>
    public Window? GetMainWindow(TimeSpan? timeout = null)
    {
        if (MainWindow != null && !MainWindow.IsOffscreen) return MainWindow;

        var searchTimeout = timeout ?? TimeSpan.FromSeconds(15);
        MainWindow = SearchMainWindow(searchTimeout);
        return MainWindow;
    }

    private Window? SearchMainWindow(TimeSpan timeout)
    {
        return Retry.WhileNull(() =>
        {
            if (Application == null || Automation == null) return null;
            
            var appProcessId = Application.ProcessId;
            var desktop = Automation.GetDesktop();

            // 1. Try Find by AutomationId "MainWindow" with PID check (Most precise)
            var byId = desktop.FindFirstChild(cf => cf.ByAutomationId("MainWindow"))?.AsWindow();
            if (byId != null)
            {
                try { 
                    var pid = byId.Properties.ProcessId.Value;
                    if (pid == appProcessId) return byId; 
                } catch { }
            }

            // 2. Try Find by Title Pattern with PID check (Multilingual support)
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                try {
                    if (w.Properties.ProcessId.Value == appProcessId)
                    {
                        var name = w.Name;
                        if (name != null && (name.Contains("Cash") || name.Contains("シミュレーター")))
                        {
                            return w.AsWindow();
                        }
                    }
                } catch { }
            }

            // 3. Fallback: Any visible window for this PID
            foreach (var w in windows)
            {
                try {
                    if (w.Properties.ProcessId.Value == appProcessId && !w.IsOffscreen) return w.AsWindow();
                } catch { }
            }

            return null;
        }, timeout, TimeSpan.FromMilliseconds(1000)).Result;
    }

    /// <summary>アプリケーションとオートメーションリソースを安全に解放し、プロセスをクリーンアップする。</summary>
    public void Dispose()
    {
        try
        {
            try
            {
                // [STABILITY] Explicitly find and close ALL windows belonging to this process
                if (Application != null && Automation != null)
                {
                    try
                    {
                        var desktop = Automation.GetDesktop();
                        var appProcessId = Application.ProcessId;
                        
                        // Retry finding windows a few times to catch any popping up during closure
                        for (int i = 0; i < 2; i++)
                        {
                            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                            foreach (var window in windows)
                            {
                                try
                                {
                                    if (window.Properties.ProcessId.Value == appProcessId)
                                    {
                                        var win = window.AsWindow();
                                        win.Close();
                                    }
                                }
                                catch { }
                            }
                            if (i == 0) Thread.Sleep(300);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (Application != null)
            {
                int pid = Application.ProcessId;
                try
                {
                    if (!Application.HasExited)
                    {
                        // 1. Try closing main window
                        if (MainWindow != null)
                        {
                            try { MainWindow.Close(); } catch { }
                        }
                        
                        // 2. Try closing Application
                        try { Application.Close(); } catch { }
                        
                        // 3. Short wait for clean exit
                        Thread.Sleep(500);
                    }
                }
                catch { }

                // 4. Forceful kill if still alive
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Kill entire process tree
                        process.WaitForExit(2000);
                    }
                }
                catch (ArgumentException) { /* Already exited */ }
                catch (Exception ex)
                {
                    Console.WriteLine($"[INFO] Failed to kill process {pid}: {ex.Message}");
                }
                
                try { Application.Dispose(); } catch { }
            }

            // Final pause to let the OS/UIA clean up traces
            Thread.Sleep(UITestTimings.AppCleanupDelayMs);
        }
        catch { }
        finally
        {
            Application = null!;
            Automation = null!;
            MainWindow = null;
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>残存している可能性のあるアプリケーションプロセス（ゾンビプロセス）を強制終了する。</summary>
    private void KillZombieProcesses()
    {
        try
        {
            // 1. Use the shared cleanup helper for standard app processes
            TestProcessCleanup.KillAllRunningProcesses();

            // 2. [CI RESILIENCE] Ensure no stale automation hosts are hanging
            // Use taskkill to ensure it's gone.
            var searchNames = new[] { "CashChangerSimulator.UI.Wpf" };
            foreach (var name in searchNames)
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /IM {name}.exe /T",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[INFO] KillZombieProcesses encountered an error (ignoring): {ex.Message}");
        }
    }
}
