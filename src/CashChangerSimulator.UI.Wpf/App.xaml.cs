using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DIContainer.Initialize();
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

