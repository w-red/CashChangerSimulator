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
/// <param name="simulateDeviceError">デバイスエラーをシミュレートするコマンド。</param>
/// <param name="resetError">エラーをリセットするコマンド。</param>
/// <param name="isJammed">ジャムが発生しているかどうかを監視するリアクティブプロパティ。</param>
/// <param name="isOverlapped">重なりが発生しているかどうかを監視するリアクティブプロパティ。</param>
/// <param name="isDeviceError">デバイスエラーが発生しているかどうかを監視するリアクティブプロパティ。</param>
public class BulkAmountInputViewModel(
    IEnumerable<BulkAmountInputItemViewModel> items,
    ICommand simulateOverlap,
    ICommand simulateJam,
    ICommand simulateDeviceError,
    ICommand resetError,
    ReadOnlyReactiveProperty<bool> isJammed,
    ReadOnlyReactiveProperty<bool> isOverlapped,
    ReadOnlyReactiveProperty<bool> isDeviceError)
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    /// <summary>入力項目のリスト。</summary>
    public IEnumerable<BulkAmountInputItemViewModel> Items { get; } = EnsureNotNull(items);

    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateOverlapCommand { get; } = EnsureNotNull(simulateOverlap);

    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateJamCommand { get; } = EnsureNotNull(simulateJam);

    /// <summary>デバイスエラーをシミュレートするコマンド。</summary>
    public ICommand SimulateDeviceErrorCommand { get; } = EnsureNotNull(simulateDeviceError);

    /// <summary>エラー状態を解消するコマンド。</summary>
    public ICommand ResetErrorCommand { get; } = EnsureNotNull(resetError);

    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; } = EnsureNotNull(isJammed);

    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; } = EnsureNotNull(isOverlapped);

    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsDeviceError { get; } = EnsureNotNull(isDeviceError);
}
