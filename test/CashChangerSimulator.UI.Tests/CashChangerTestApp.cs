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

        // Adjust this path based on your project structure and build output
        var potentialPath = Path.GetFullPath(Path.Combine(assemblyDir, "../../../../../src/CashChangerSimulator.UI.Wpf/bin/Debug/net10.0-windows/CashChangerSimulator.UI.Wpf.exe"));

        if (!File.Exists(potentialPath))
        {
            // Fallback or throw
            throw new FileNotFoundException($"Application executable not found at {potentialPath}. Ensure the application is built.");
        }

        _executablePath = potentialPath;
    }

    public void Launch(string? customConfigToml = null, bool hotStart = true)
    {
        string fullPath = Path.GetFullPath(_executablePath);
        string? appDir = Path.GetDirectoryName(fullPath);

        // Clean up state files to ensure a fresh start for each test
        if (appDir != null)
        {
            var filesToClean = new[] { "inventory.toml", "history.bin", "config.toml", "history.bin" };
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
        Application = Application.Launch(startInfo);

        // Use a more robust wait for the window
        MainWindow = Retry.WhileNull(() =>
        {
            var win = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(5));
            if (win != null) return win;

            // Fallback: Robust search on desktop
            var desktop = Automation.GetDesktop();
            // Try by AutomationId first (more reliable)
            var mainWindowById = desktop.FindFirstChild(cf => cf.ByAutomationId("MainWindow"));
            if (mainWindowById != null) return mainWindowById.AsWindow();

            // Try by Title
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
        }, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(2000)).Result ?? throw new Exception("Main window 'Cash Changer Simulator' (or ID 'MainWindow') not found after 30 seconds.");
        MainWindow.WaitUntilClickable(TimeSpan.FromSeconds(10));
        MainWindow.SetForeground();
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
            MainWindow?.Close();
        }
        catch { }

        // Dispose Automation BEFORE closing the Application to avoid COM issues
        Automation?.Dispose();

        try
        {
            if (Application != null)
            {
                var processId = Application.ProcessId;
                if (!Application.HasExited)
                {
                    Application.Close();
                }

                // Force kill if it doesn't close within 3 seconds
                try
                {
                    using var process = System.Diagnostics.Process.GetProcessById(processId);
                    if (!process.HasExited && !process.WaitForExit(3000))
                    {
                        process.Kill(true); // Kill entire process tree
                        process.WaitForExit(1000);
                    }
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                
                Application.Dispose();
            }
        }
        catch { }

        // Final pause to let the OS/UIA clean up traces
        Thread.Sleep(UITestTimings.AppCleanupDelayMs);
        GC.SuppressFinalize(this);
    }
}
