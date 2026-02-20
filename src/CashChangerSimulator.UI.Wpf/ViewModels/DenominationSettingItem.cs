using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>金種ごとの詳細設定を保持・管理するデータ項目。</summary>
public class DenominationSettingItem : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>対象の金種キー。</summary>
    public DenominationKey Key { get; }
    /// <summary>個別の表示名。</summary>
    public BindableReactiveProperty<string> DisplayName { get; }
    /// <summary>初期在庫枚数。</summary>
    public BindableReactiveProperty<int> Count { get; }
    /// <summary>NearEmpty しきい値。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }
    /// <summary>NearFull しきい値。</summary>
    public BindableReactiveProperty<int> NearFull { get; }
    /// <summary>Full しきい値。</summary>
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

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
