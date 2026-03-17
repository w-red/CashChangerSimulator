using System;
using System.Threading.Tasks;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>
/// UI スレッドへのアクセスを抽象化するサービス。
/// ユニットテスト環境（Dispatcher が存在しない環境）でのハングアップを防ぐために使用します。
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// 必要に応じて UI スレッドでアクションを実行します。
    /// </summary>
    void SafeInvoke(Action action);

    /// <summary>
    /// UI スレッドで非同期にアクションを実行します。
    /// </summary>
    Task InvokeAsync(Action action);
}
