namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// POS取引モードの状態を表す列挙型。
/// </summary>
public enum PosTransactionStatus
{
    Idle,
    WaitingForCash,
    DispensingChange,
    Completed
}
