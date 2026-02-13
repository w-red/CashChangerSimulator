using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace CashChangerSimulator.UI.Tests;

public class CashChangerTestApp : IDisposable
{
    public Application Application { get; private set; } = null!;
    public UIA3Automation Automation { get; private set; } = null!;
    public Window MainWindow { get; private set; } = null!;
    
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
        Application = Application.Launch(_executablePath);
        Automation = new UIA3Automation();
        
        // Use standard way first with longer timeout
        MainWindow = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(20));
        
        // Fallback: Robust search on desktop
        if (MainWindow == null)
        {
            var desktop = Automation.GetDesktop();
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            foreach (var win in windows)
            {
                try {
                    if (win.Name.Contains("Cash Changer Simulator"))
                    {
                        MainWindow = win.AsWindow();
                        break;
                    }
                } catch { }
            }
        }

        if (MainWindow == null)
        {
             throw new Exception("Main window 'Cash Changer Simulator' not found.");
        }

        MainWindow.WaitUntilClickable(TimeSpan.FromSeconds(10));
    }

    public void Dispose()
    {
        Automation?.Dispose();
        Application?.Close();
        Application?.Dispose();
    }
}
