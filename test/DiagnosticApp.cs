using System;
using System.Reflection;
using System.Windows;
using CashChangerSimulator.Core;
using CashChangerSimulator.UI.Wpf;

namespace Diagnostic
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var wpfAssembly = Assembly.Load("CashChangerSimulator.UI.Wpf");
                var appType = wpfAssembly.GetType("CashChangerSimulator.UI.Wpf.App");
                var app = Activator.CreateInstance(appType) as Application;
                
                // We don't call Run() because it blocks. 
                // We just want to see if we can create the window and measure it.
                
                DIContainer.Initialize();
                
                var mainWindowType = wpfAssembly.GetType("CashChangerSimulator.UI.Wpf.Views.MainWindow");
                var window = Activator.CreateInstance(mainWindowType) as Window;
                
                window.Show();
                window.UpdateLayout(); 
                
                Console.WriteLine("SUCCESS: MainWindow loaded and layout updated without exception.");
                window.Close();
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILURE: " + ex.GetType().Name);
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                if (ex.InnerException != null)
                {
                    Console.WriteLine("INNER: " + ex.InnerException.Message);
                }
                Environment.Exit(1);
            }
        }
    }
}
