using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using FlaUI.Core.Tools;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace CashChangerSimulator.UI.Tests;

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

    public void Launch()
    {
        string fullPath = Path.GetFullPath(_executablePath);
        Console.WriteLine($"[CashChangerTestApp] Launching: {fullPath}");
        
        // Start fresh
        Automation = new UIA3Automation();
        Application = Application.Launch(_executablePath);
        
        // Use a more robust wait for the window
        MainWindow = Retry.WhileNull(() =>
        {
            var win = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(5));
            if (win != null) return win;

            // Fallback: Robust search on desktop if Application object fails
            var desktop = Automation.GetDesktop();
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
        }, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(500)).Result;

        if (MainWindow == null)
        {
            throw new Exception("Main window 'Cash Changer Simulator' not found after 30 seconds.");
        }

        MainWindow.WaitUntilClickable(TimeSpan.FromSeconds(10));
        MainWindow.SetForeground();
    }

    public void Dispose()
    {
        try
        {
            // Close window explicitly if possible
            MainWindow?.Close();
        }
        catch { }

        // Dispose Automation BEFORE closing the Application to avoid COM issues
        Automation?.Dispose();

        try
        {
            if (Application != null && !Application.HasExited)
            {
                Application.Close();
                Application.Dispose();
            }
        }
        catch { }
    }
}
