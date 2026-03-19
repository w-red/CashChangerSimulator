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
    public Application Application { get; private set; } = null!;
    public UIA3Automation Automation { get; private set; } = null!;
    public Window? MainWindow { get; private set; }
    private readonly string _executablePath;

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

        // Adjust this path based on your project structure and build output
        var relativePath = $"../../../../../src/CashChangerSimulator.UI.Wpf/bin/{config}/net10.0-windows/CashChangerSimulator.UI.Wpf.exe";
        var potentialPath = Path.GetFullPath(Path.Combine(assemblyDir, relativePath));

        // Fallback: Check the other configuration just in case
        if (!File.Exists(potentialPath))
        {
            string otherConfig = (config == "Debug") ? "Release" : "Debug";
            var fallbackPath = Path.GetFullPath(Path.Combine(assemblyDir, $"../../../../../src/CashChangerSimulator.UI.Wpf/bin/{otherConfig}/net10.0-windows/CashChangerSimulator.UI.Wpf.exe"));
            if (File.Exists(fallbackPath))
            {
                potentialPath = fallbackPath;
            }
        }

        if (!File.Exists(potentialPath))
        {
            // Fallback or throw
            throw new FileNotFoundException($"Application executable not found. Tried: {potentialPath}. Ensure the application is built.");
        }

        _executablePath = potentialPath;
    }

    public void Launch(string? customConfigToml = null, bool hotStart = true, Dictionary<string, string>? envVars = null)
    {
        // [STABILITY] Ensure previous session is cleaned up
        Dispose();
        TestProcessCleanup.KillAllRunningProcesses();

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
            Thread.Sleep(2000);

            // Use a more robust wait for the window
            var isCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            var windowWaitTimeout = isCi ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(20);

            MainWindow = Retry.WhileNull(() =>
            {
                // Try standard Application API
                var win = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(1));
                if (win != null) return win;

                // Fallback: Proactive desktop scan
                var desktop = Automation.GetDesktop();
                
                // Priority 1: By AutomationId
                var byId = desktop.FindFirstChild(cf => cf.ByAutomationId("MainWindow"));
                if (byId != null) return byId.AsWindow();

                // Priority 2: By Name/Title for this process
                var appProcessId = Application.ProcessId;
                var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                var byProcess = allWindows.FirstOrDefault(w => 
                    w.Properties.ProcessId == appProcessId && 
                    (w.Name.Contains("Cash Changer") || w.Name.Contains("シミュレーター")));
                
                return byProcess?.AsWindow();
            }, windowWaitTimeout, TimeSpan.FromMilliseconds(2000)).Result;

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

            if (!isCi)
            {
                try
                {
                    MainWindow.SetForeground();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[INFO] SetForeground failed: {ex.Message}");
                }
            }

            // Skip clickable wait in CI as it often hangs
            if (!isCi)
            {
                var timeout = TimeSpan.FromSeconds(10);
                try
                {
                    MainWindow.WaitUntilClickable(timeout);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[WARNING] WaitUntilClickable failed: {ex.Message}. Proceeding anyway as window is found.");
                    
                    bool isOffscreen = false;
                    try
                    {
                        isOffscreen = MainWindow.IsOffscreen;
                    }
                    catch
                    {
                        Console.WriteLine("[INFO] IsOffscreen property access failed/not supported.");
                    }

                    if (isOffscreen)
                    {
                        throw new Exception("Main window is offscreen and could not be made clickable.");
                    }
                }
            }
            else
            {
                Console.WriteLine("[INFO] Skipping WaitUntilClickable in CI environment.");
            }
        }
        catch (Exception)
        {
            // 起動中に失敗した場合、中途半端に起動したプロセスを掃除してから例外を投げ直す
            Dispose();
            throw;
        }
    }

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

            try
            {
                if (MainWindow != null)
                {
                    MainWindow.Close();
                    Thread.Sleep(200); // Short wait
                }
            }
            catch { }

            // Dispose Automation BEFORE closing the Application to avoid COM issues
            try { Automation?.Dispose(); } catch { }

            try
            {
                if (Application != null)
                {
                    int pid = Application.ProcessId;
                    if (!Application.HasExited)
                    {
                        try { Application.Close(); } catch { }
                        
                        // Wait up to 1 seconds for clean exit
                        for (int i = 0; i < 5; i++)
                        {
                            if (Application.HasExited) break;
                            Thread.Sleep(200);
                        }
                    }

                    // If still running, kill it forcefully along with its children
                    if (!Application.HasExited)
                    {
                        try
                        {
                            // Use a safer way to get the process to avoid masking the original exception
                            var process = System.Diagnostics.Process.GetProcesses().FirstOrDefault(p => p.Id == pid);
                            if (process != null && !process.HasExited)
                            {
                                process.Kill(true); // Kill entire process tree
                                process.WaitForExit(1000);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[INFO] Failed to kill process {pid}: {ex.Message}");
                        }
                    }
                    
            try { Application.Dispose(); } catch { }
                }
            }
            catch { }

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

    private void KillOrphanedProcesses()
    {
        try
        {
            var appName = Path.GetFileNameWithoutExtension(_executablePath);
            var processes = System.Diagnostics.Process.GetProcessesByName(appName);
            foreach (var p in processes)
            {
                try
                {
                    p.Kill(true);
                    p.WaitForExit(1000);
                }
                catch { }
            }
        }
        catch { }
    }
}
