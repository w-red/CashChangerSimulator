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
            // [TEST STABILITY] Auto-open device only when hotStart=true to allow ColdStartUITest to verify restricted UI.
            startInfo.EnvironmentVariables["TEST_AUTO_OPEN_DEVICE"] = hotStart ? "True" : "False";

            if (envVars != null)
            {
                foreach (var kvp in envVars)
                {
                    startInfo.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }
            Application = Application.Launch(startInfo);
            
            // [STABILITY] Wait for process and UI to settle (increased for environment readiness)
            Thread.Sleep(7000);

            // Use a more robust wait for the window
            var isCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            var windowWaitTimeout = TimeSpan.FromSeconds(60);

            Console.WriteLine($"[TEST] Starting MainWindow search (Timeout: {windowWaitTimeout.TotalSeconds}s, PID: {Application.ProcessId})...");
            MainWindow = SearchMainWindow(windowWaitTimeout);

            if (MainWindow == null)
            {
                var hasExited = Application.HasExited;
                var exitCode = hasExited ? Application.ExitCode : (int?)null;
                Console.WriteLine($"[ERROR] Main window not found. Process Status: HasExited={hasExited}, ExitCode={exitCode}");

                // Dump ALL windows for debugging if it fails
                try
                {
                    var all = Automation.GetDesktop().FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                    foreach (var w in all) Console.WriteLine($"- Global Window: Name='{w.Name}', ID='{w.AutomationId}', PID={w.Properties.ProcessId}");
                } catch { }

                throw new Exception($"Main window 'Cash Changer Simulator' (or ID 'MainWindowRoot') not found. App PID: {Application.ProcessId}, HasExited: {hasExited}, ExitCode: {exitCode}");
            }

            // Settlement period for UI Automation state
            Thread.Sleep(500);

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
        Console.WriteLine($"[TEST] Polling for MainWindow... (Timeout: {timeout.TotalSeconds}s)");
        return Retry.WhileNull(() =>
        {
            try
            {
                if (Application == null || Automation == null) return null;
                
                var appProcessId = Application.ProcessId;
                var desktop = Automation.GetDesktop();
                var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));

                Console.WriteLine($"[DEBUG-UI] Polling... AppPID={appProcessId}, WindowsCount={windows.Length}");

                // 1. Try Find by Name/Title Pattern with PID check (Most robust)
                foreach (var w in windows)
                {
                    try {
                        if (w.Properties.ProcessId.ValueOrDefault == appProcessId)
                        {
                            var name = w.Properties.Name.ValueOrDefault;
                            if (name != null && (name.Contains("Cash") || name.Contains("Simulator") || name.Contains("シミュレーター")))
                            {
                                Console.WriteLine($"[TEST] Found MainWindow by Title: {name} (PID: {appProcessId})");
                                var win = w.AsWindow();
                                Thread.Sleep(3000); 
                                return win;
                            }
                        }
                    } catch { }
                }

                // 2. Try Find by AutomationId "MainWindowRoot" with PID check
                foreach (var w in windows)
                {
                    try {
                        if (w.Properties.AutomationId.ValueOrDefault == "MainWindowRoot")
                        {
                            var pid = w.Properties.ProcessId.ValueOrDefault;
                            if (pid == appProcessId)
                            {
                                Console.WriteLine($"[TEST] Found MainWindow by AutomationId: {w.Name} (PID: {pid})");
                                var win = w.AsWindow();
                                Thread.Sleep(3000); 
                                return win;
                            }
                        }
                    } catch { }
                }

                // 3. Fallback: Any visible window for this PID
                foreach (var w in windows)
                {
                    try {
                        if (w.Properties.ProcessId.ValueOrDefault == appProcessId && !w.IsOffscreen)
                        {
                            Console.WriteLine($"[TEST] Found fallback window for PID {appProcessId}: {w.Name}");
                            var win = w.AsWindow();
                            Thread.Sleep(3000);
                            return win;
                        }
                    } catch { }
                }

                // 4. Ultimate Fallback: Any window with "MainWindowRoot" globally (Ignore PID)
                var globalId = desktop.FindFirstDescendant(cf => cf.ByAutomationId("MainWindowRoot"))?.AsWindow();
                if (globalId != null)
                {
                    Console.WriteLine($"[TEST] Found MainWindow globally by AutomationId (PID mismatch ignored): {globalId.Properties.ProcessId.ValueOrDefault}");
                    Thread.Sleep(3000);
                    return globalId;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG-UI] Error during polling: {ex.Message}");
            }

            return null;
        }, timeout, TimeSpan.FromMilliseconds(5000)).Result;
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
