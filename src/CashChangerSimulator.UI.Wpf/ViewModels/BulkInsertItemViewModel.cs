using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>バルク投入（一括投入）用の一時アイテム ViewModel。</summary>
public class BulkInsertItemViewModel(DenominationKey key, string name)
{
    /// <summary>金種キー。</summary>
    public DenominationKey Key { get; } = key;
    /// <summary>表示名。</summary>
    public string Name { get; } = name;
    /// <summary>投入（または排出）する数。</summary>
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
