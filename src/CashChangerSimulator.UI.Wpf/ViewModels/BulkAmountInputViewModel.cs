using R3;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 一括入力ダイアログ用の ViewModel。
/// 項目リストと、親 ViewModel から引き継いだシミュレーションコマンドを保持します。
/// </summary>
public class BulkAmountInputViewModel(
    IEnumerable<BulkAmountInputItemViewModel> items,
    ICommand simulateOverlap,
    ICommand simulateJam,
    ICommand resetError,
    ReadOnlyReactiveProperty<bool> isJammed,
    ReadOnlyReactiveProperty<bool> isOverlapped)
{
    /// <summary>入力項目のリスト。</summary>
    public IEnumerable<BulkAmountInputItemViewModel> Items { get; } = items;

    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateOverlapCommand { get; } = simulateOverlap;

    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateJamCommand { get; } = simulateJam;

    /// <summary>エラー状態を解消するコマンド。</summary>
    public ICommand ResetErrorCommand { get; } = resetError;

    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; } = isJammed;

    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; } = isOverlapped;
}
