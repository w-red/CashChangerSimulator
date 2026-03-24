using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>入金操作の実行ロジックを担当するサービスのインターフェース。</summary>
public interface IDepositOperationService
{
    /// <summary>入金処理を開始します。</summary>
    void BeginDeposit();

    /// <summary>入金を一時停止または再開します。</summary>
    /// <param name="pause">一時停止または再開のアクション。</param>
    void PauseDeposit(CashDepositPause pause);

    /// <summary>入金を確定します。</summary>
    void FixDeposit();

    /// <summary>入金処理を終了（収納または返却）します。</summary>
    /// <param name="action">終了時のアクション。</param>
    void EndDeposit(CashDepositAction action);

    /// <summary>クイック入金を実行します。</summary>
    /// <param name="targetAmount">投入目標額。</param>
    /// <param name="availableDenominations">投入可能な金種キーのリスト。</param>
    Task ExecuteQuickDepositAsync(decimal targetAmount, IEnumerable<DenominationKey> availableDenominations);

    /// <summary>リジェクトをシミュレートします。</summary>
    /// <param name="amount">リジェクト額。</param>
    void SimulateReject(decimal amount);

    /// <summary>一括投入を追跡します。</summary>
    /// <param name="counts">投入された金種と枚数の辞書。</param>
    void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts);
}
