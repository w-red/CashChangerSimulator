namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// POS取引モードの状態を表す列挙型。
/// </summary>
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
