using R3;
using System.Windows.Input;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>一括入力ダイアログ（Bulk Input）を制御する ViewModel。</summary>
/// <remarks>
/// 各金種の入力項目リストを保持し、ダイアログからのシミュレーション操作（ジャム・重なり・エラー解除）を、
/// 親 ViewModel から注入されたコマンドを通じて実行します。
/// </remarks>
/// <param name="items">金種ごとの入力項目リスト。</param>
/// <param name="simulateOverlap">重なりエラーをシミュレートするコマンド。</param>
/// <param name="simulateJam">ジャムエラーをシミュレートするコマンド。</param>
/// <param name="resetError">エラーをリセットするコマンド。</param>
/// <param name="isJammed">ジャムが発生しているかどうかを監視するリアクティブプロパティ。</param>
/// <param name="isOverlapped">重なりが発生しているかどうかを監視するリアクティブプロパティ。</param>
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
