using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// WPF の Dispatcher を使用した IDispatcherService の実装。
/// </summary>
public class WpfDispatcherService : IDispatcherService
{
    public void SafeInvoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public Task InvokeAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null)
        {
            return dispatcher.InvokeAsync(action).Task;
        }
        
        action();
        return Task.CompletedTask;
    }
}
