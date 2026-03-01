using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>金種ごとの詳細設定を保持・管理するデータ項目。</summary>
public class DenominationSettingItem : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>対象の金種キー。</summary>
    public DenominationKey Key { get; }
    /// <summary>英語の表示名。</summary>
    public BindableReactiveProperty<string> DisplayName { get; }
    /// <summary>日本語の表示名。</summary>
    public BindableReactiveProperty<string> DisplayNameJP { get; }
    /// <summary>初期在庫枚数。</summary>
    public BindableReactiveProperty<int> Count { get; }
    /// <summary>NearEmpty しきい値。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }
    /// <summary>NearFull しきい値。</summary>
    public BindableReactiveProperty<int> NearFull { get; }
    /// <summary>Full しきい値。</summary>
    public BindableReactiveProperty<int> Full { get; }
    /// <summary>釣銭リサイクルに使用されるかどうか。</summary>
    public BindableReactiveProperty<bool> IsRecyclable { get; }

    public DenominationSettingItem(
        DenominationKey key,
        string displayName,
        string displayNameJP,
        int count,
        int nearEmpty,
        int nearFull,
        int full,
        bool isRecyclable)
    {
        Key = key;
        DisplayName = new BindableReactiveProperty<string>(displayName).AddTo(_disposables);
        DisplayNameJP = new BindableReactiveProperty<string>(displayNameJP).AddTo(_disposables);
        Count = new BindableReactiveProperty<int>(count).AddTo(_disposables);
        NearEmpty = new BindableReactiveProperty<int>(nearEmpty).AddTo(_disposables);
        NearFull = new BindableReactiveProperty<int>(nearFull).AddTo(_disposables);
        Full = new BindableReactiveProperty<int>(full).AddTo(_disposables);
        IsRecyclable = new BindableReactiveProperty<bool>(isRecyclable).AddTo(_disposables);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
