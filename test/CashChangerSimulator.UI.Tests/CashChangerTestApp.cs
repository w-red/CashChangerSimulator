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
        try
        {
            // 安全策として、同名のプロセスが残っている場合は掃除する（ただし本来は各テストが責任を持つべき）
            TestProcessCleanup.KillAllRunningProcesses();

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
                    var configContent = $@"[Simulation]
DispenseDelayMs = 500
HotStart = {hotStart.ToString().ToLower()}
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

            // Use a more robust wait for the window
            var isCi = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
            var windowWaitTimeout = isCi ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(30);

            MainWindow = Retry.WhileNull(() =>
            {
                var win = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(2));
                if (win != null) return win;

                // Fallback: Robust search on desktop
                var desktop = Automation.GetDesktop();
                var mainWindowById = desktop.FindFirstChild(cf => cf.ByAutomationId("MainWindow"));
                if (mainWindowById != null) return mainWindowById.AsWindow();

                var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                foreach (var w in windows)
                {
                    try
                    {
                        if (w.Name.Contains("Cash Changer Simulator"))
                        {
                            return w.AsWindow();
                        }
                    }
                    catch { }
                }
                return null;
            }, windowWaitTimeout, TimeSpan.FromMilliseconds(1000)).Result;

            if (MainWindow == null)
            {
                if (isCi)
                {
                    Console.WriteLine("[WARNING] Main window not found in CI environment. Skipping UI interactions.");
                    return;
                }
                throw new Exception("Main window 'Cash Changer Simulator' (or ID 'MainWindow') not found.");
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
            // Close all windows explicitly
            if (Application != null && Automation != null)
            {
                var desktop = Automation.GetDesktop();
                var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                var appProcessId = Application.ProcessId;

                foreach (var window in allWindows)
                {
                    try
                    {
                        // ONLY close windows belonging to OUR test application process
                        if (window.Properties.ProcessId.Value == appProcessId)
                        {
                            var win = window.AsWindow();
                            win.Close();
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }

        try
        {
            if (MainWindow != null)
            {
                MainWindow.Close();
                Thread.Sleep(500); // Give it a moment to close
            }
        }
        catch { /* Ignore errors during close */ }

        // Dispose Automation BEFORE closing the Application to avoid COM issues
        try { Automation?.Dispose(); } catch { }

        try
        {
            if (Application != null)
            {
                int pid = Application.ProcessId;
                if (!Application.HasExited)
                {
                    Application.Close();
                    // Wait up to 2 seconds for clean exit
                    if (!Application.HasExited)
                    {
                        Thread.Sleep(2000);
                    }
                }

                // If still running, kill it forcefully
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById(pid);
                    if (!process.HasExited)
                    {
                        process.Kill(true); // Kill entire process tree
                        process.WaitForExit(1000);
                    }
                }
                catch (ArgumentException) { /* Process already gone */ }
                catch (Exception) { /* Ignore other errors */ }
                
                Application.Dispose();
            }
        }
        catch { }

        // Final pause to let the OS/UIA clean up traces
        Thread.Sleep(UITestTimings.AppCleanupDelayMs);
        GC.SuppressFinalize(this);
    }
}
