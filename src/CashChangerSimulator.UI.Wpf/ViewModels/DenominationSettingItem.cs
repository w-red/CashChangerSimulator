using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>金種ごとの詳細設定を保持・管理するデータ項目。</summary>
public class DenominationSettingItem : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    public DenominationKey Key { get; }

    public BindableReactiveProperty<string> DisplayName { get; }
    public BindableReactiveProperty<int> Count { get; }
    public BindableReactiveProperty<int> NearEmpty { get; }
    public BindableReactiveProperty<int> NearFull { get; }
    public BindableReactiveProperty<int> Full { get; }

    public DenominationSettingItem(
        DenominationKey key,
        string displayName,
        int count,
        int nearEmpty,
        int nearFull,
        int full)
    {
        Key = key;
        DisplayName = new BindableReactiveProperty<string>(displayName).AddTo(_disposables);
        Count = new BindableReactiveProperty<int>(count).AddTo(_disposables);
        NearEmpty = new BindableReactiveProperty<int>(nearEmpty).AddTo(_disposables);
        NearFull = new BindableReactiveProperty<int>(nearFull).AddTo(_disposables);
        Full = new BindableReactiveProperty<int>(full).AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
