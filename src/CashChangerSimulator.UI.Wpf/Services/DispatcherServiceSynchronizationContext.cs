using System;
using System.Threading;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// IDispatcherService をラップし、R3 や他の非同期コンポーネントで利用可能な 
/// SynchronizationContext を提供します。
/// </summary>
public class DispatcherServiceSynchronizationContext : SynchronizationContext
{
    private readonly IDispatcherService _dispatcher;

    public DispatcherServiceSynchronizationContext(IDispatcherService dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        _dispatcher.SafeInvokeAsync(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        _dispatcher.SafeInvoke(() => d(state));
    }

    public override SynchronizationContext CreateCopy()
    {
        return new DispatcherServiceSynchronizationContext(_dispatcher);
    }
}
