namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>POS取引（チェックアウト）シミュレーションにおける現在の進行状態を表す列挙型。</summary>
/// <remarks>
/// 取引の開始から、対話的な現金投入、お釣りの払い出し、完了までのライフサイクルステータスを定義します。
/// </remarks>
public enum PosTransactionStatus
{
    /// <summary>待機中。</summary>
    Idle,
    /// <summary>現金投入待ち。</summary>
    WaitingForCash,
    /// <summary>お釣り払い出し中。</summary>
    DispensingChange,
    /// <summary>完了。</summary>
    Completed
}
