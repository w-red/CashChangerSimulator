using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>金種指定入出金用のアイテム ViewModel。</summary>
/// <param name="key">金種キー。</param>
/// <param name="name">表示名。</param>
public class BulkAmountInputItemViewModel(DenominationKey key, string name)
{
    /// <summary>金種キー。</summary>
    public DenominationKey Key { get; } = key;
    /// <summary>表示名。</summary>
    public string Name { get; } = name;
    /// <summary>数量。</summary>
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
