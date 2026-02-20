namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>UIテスト用の共通待機時間・タイムアウト定数。</summary>
public static class DepositTestTimings
{
    /// <summary>UI状態遷移待機時間（ミリ秒）。</summary>
    public const int UiTransitionDelayMs = 1000;

    /// <summary>ウィンドウポップアップ待機時間（ミリ秒）。</summary>
    public const int WindowPopupDelayMs = 2000;

    /// <summary>UI論理実行待機時間（ミリ秒）。</summary>
    public const int LogicExecutionDelayMs = 500;

    /// <summary>長期リトライタイムアウト。</summary>
    public static readonly TimeSpan RetryLongTimeout = TimeSpan.FromSeconds(10);

    /// <summary>短期リトライタイムアウト。</summary>
    public static readonly TimeSpan RetryShortTimeout = TimeSpan.FromSeconds(5);
}
