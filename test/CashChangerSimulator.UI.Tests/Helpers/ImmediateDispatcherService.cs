using CashChangerSimulator.UI.Wpf.Services;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>テスト用の即時実行型 DispatcherService。</summary>
public class ImmediateDispatcherService : IDispatcherService
{
    public void SafeInvoke(Action action)
    {
        action();
    }

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public object? GetActiveWindow()
    {
        return null;
    }
}
