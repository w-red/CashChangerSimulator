using System;
using System.Windows;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Views;
using CashChangerSimulator.Core;

namespace Diagnostic
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var app = new App();
                // We don't call Run() because it blocks. 
                // We just want to see if we can create the window and measure it.
                
                DIContainer.Initialize();
                
                var window = new MainWindow();
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
