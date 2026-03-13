namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>POS取引シミュレーションで使用する定数定義。</summary>
public static class PosTransactionConstants
{
    /// <summary>Claim 時のデフォルトタイムアウト（ミリ秒）。</summary>
    public const int DefaultClaimTimeout = 1000;

    /// <summary>取引完了メッセージ表示後のリセットまでの待機時間（ミリ秒）。</summary>
    public const int CompletionResetDelay = 3000;

    /// <summary>出金完了後の待機時間（ミリ秒）。</summary>
    public const int DispenseWaitDelay = 1000;
}
