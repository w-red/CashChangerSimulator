using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>バルク投入（一括投入）用の一時アイテム ViewModel。</summary>
public class BulkInsertItemViewModel(DenominationKey key, string name)
{
    public DenominationKey Key { get; } = key;
    public string Name { get; } = name;
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
