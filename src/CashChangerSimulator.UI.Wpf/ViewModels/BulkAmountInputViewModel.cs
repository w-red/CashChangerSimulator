using System.Windows.Input;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 一括入力ダイアログ用の ViewModel。
/// 項目リストと、親 ViewModel から引き継いだシミュレーションコマンドを保持します。
/// </summary>
public class BulkAmountInputViewModel
{
    /// <summary>入力項目のリスト。</summary>
    public IEnumerable<BulkAmountInputItemViewModel> Items { get; }

    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateOverlapCommand { get; }
    
    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateJamCommand { get; }
    
    /// <summary>エラー状態を解消するコマンド。</summary>
    public ICommand ResetErrorCommand { get; }

    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; }
    
    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; }

    public BulkAmountInputViewModel(
        IEnumerable<BulkAmountInputItemViewModel> items,
        ICommand simulateOverlap,
        ICommand simulateJam,
        ICommand resetError,
        ReadOnlyReactiveProperty<bool> isJammed,
        ReadOnlyReactiveProperty<bool> isOverlapped)
    {
        Items = items;
        SimulateOverlapCommand = simulateOverlap;
        SimulateJamCommand = simulateJam;
        ResetErrorCommand = resetError;
        IsJammed = isJammed;
        IsOverlapped = isOverlapped;
    }
}
