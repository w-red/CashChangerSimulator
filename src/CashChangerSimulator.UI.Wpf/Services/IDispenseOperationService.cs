using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>出金操作の実行ロジックを担当するサービスのインターフェース。</summary>
public interface IDispenseOperationService
{
    /// <summary>指定額の払い出しを実行します。</summary>
    /// <param name="amount">払い出し金額。</param>
    void DispenseCash(decimal amount);

    /// <summary>金種構成を指定して払い出しを実行します。</summary>
    /// <param name="counts">払い出す金種と枚数の辞書。</param>
    void ExecuteBulkDispense(IReadOnlyDictionary<DenominationKey, int> counts);
}
