using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.Views;
using MaterialDesignThemes.Wpf;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// MaterialDesign の DialogHost を使用して通知を表示するサービス。
/// </summary>
public class WpfNotifyService : INotifyService
{
    private readonly IDispatcherService _dispatcher;

    public WpfNotifyService(IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void ShowWarning(string message, string title = "Warning") => ShowDialog(message, title);
    public void ShowError(string message, string title = "Error") => ShowDialog(message, title);
    public void ShowInfo(string message, string title = "Info") => ShowDialog(message, title);

    private void ShowDialog(string message, string title)
    {
        var dialog = new MessageDialog
        {
            Title = title,
            Message = message
        };

        // UI スレッドで実行
        _dispatcher.SafeInvoke(() =>
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var activeWindow = app.Windows.OfType<System.Windows.Window>().FirstOrDefault(x => x.IsActive) 
                               ?? app.MainWindow;

            if (activeWindow == null) return;

            // Try to find a DialogHost in the active window.
            var dialogHost = FindVisualChild<DialogHost>(activeWindow);

            string dialogIdentifier = "RootDialog";
            if (dialogHost != null && !string.IsNullOrEmpty(dialogHost.Identifier as string))
            {
                dialogIdentifier = (string)dialogHost.Identifier;
            }

            DialogHost.Show(dialog, dialogIdentifier);
        });
    }

    /// <summary>
    /// Helper to find a visual child of a specified type.
    /// </summary>
    private static T? FindVisualChild<T>(System.Windows.DependencyObject? parent) where T : System.Windows.DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child != null && child is T t)
            {
                return t;
            }
            else
            {
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
        }
        return null;
    }
}
