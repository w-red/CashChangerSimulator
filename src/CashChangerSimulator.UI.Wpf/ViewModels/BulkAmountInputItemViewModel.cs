using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>金種指定入出金用のアイテム ViewModel。</summary>
/// <param name="key">金種を一意に識別する <see cref="DenominationKey"/>。</param>
/// <param name="name">表示用の名称（例：1000円札）。</param>
public class BulkAmountInputItemViewModel
(DenominationKey key, string name)
{
    private static T EnsureNotNull<T>(T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }

    /// <summary>金種キー。</summary>
    public DenominationKey Key { get; } = EnsureNotNull(key);

    /// <summary>表示名。</summary>
    public string Name { get; } = EnsureNotNull(name);

    /// <summary>入力された数量。</summary>
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
