using System.Windows;
using System.Windows.Threading;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// WPF の Dispatcher を使用した IDispatcherService の実装。
/// </summary>
public class WpfDispatcherService : IDispatcherService
{
    private readonly Dispatcher _dispatcher;

    public WpfDispatcherService()
    {
        // Capture the dispatcher of the thread creating this service (expected to be the UI thread)
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void SafeInvoke(Action action)
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void SafeInvokeAsync(Action action)
    {
        if (_dispatcher != null)
        {
            _dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public Task InvokeAsync(Action action)
    {
        if (_dispatcher != null)
        {
            return _dispatcher.InvokeAsync(action).Task;
        }
        
        action();
        return Task.CompletedTask;
    }

    public object? GetActiveWindow()
    {
        return Application.Current?.MainWindow;
    }
}
