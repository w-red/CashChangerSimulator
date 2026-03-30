using CashChangerSimulator.UI.Wpf.Services;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>テスト用の即時実行型 DispatcherService。</summary>
public class ImmediateDispatcherService : IDispatcherService
{
    /// <summary>アクションを即座に実行します。</summary>
    public void SafeInvoke(Action action)
    {
        action();
    }

    /// <summary>アクションを即座に実行し、完了済みのタスクを返します。</summary>
    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    /// <summary>常に null を返します。</summary>
    public object? GetActiveWindow()
    {
        return null;
    }
}
