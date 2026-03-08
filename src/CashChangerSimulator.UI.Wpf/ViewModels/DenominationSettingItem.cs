using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>設定画面において各金種の個別パラメータを管理するためのデータ項目モデル。</summary>
/// <param name="key">金種を一意に識別する <see cref="DenominationKey"/>。</param>
/// <param name="displayName">UI 表示用の名称。</param>
/// <param name="displayNameJP">UI 表示用の日本語名称。</param>
/// <param name="count">初期枚数。</param>
/// <param name="nearEmpty">「少額（NearEmpty）」と判定するしきい値。</param>
/// <param name="nearFull">「多額（NearFull）」と判定するしきい値。</param>
/// <param name="full">「満杯（Full）」と判定するしきい値。</param>
/// <param name="isRecyclable">還流（再利用）可能かどうか。</param>
/// <remarks>
/// UI 上での編集対象となるプロパティ（表示名、初期枚数、しきい値等）を ReactiveProperty として保持します。
/// 画面上でのリアルタイムなバリデーションと、設定ファイルへの保存用データの仲介を行います。
/// </remarks>
public class DenominationSettingItem : IDisposable
{
    private readonly CompositeDisposable _disposables = [];

    /// <summary>金種キー。</summary>
    public DenominationKey Key { get; }
    /// <summary>表示名。</summary>
    public BindableReactiveProperty<string> DisplayName { get; }
    /// <summary>日本語表示名。</summary>
    public BindableReactiveProperty<string> DisplayNameJP { get; }
    /// <summary>枚数。</summary>
    public BindableReactiveProperty<int> Count { get; }
    /// <summary>少額しきい値。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }
    /// <summary>多額しきい値。</summary>
    public BindableReactiveProperty<int> NearFull { get; }
    /// <summary>満杯しきい値。</summary>
    public BindableReactiveProperty<int> Full { get; }
    /// <summary>還流可能フラグ。</summary>
    public BindableReactiveProperty<bool> IsRecyclable { get; }
    /// <summary>入金可能フラグ。</summary>
    public BindableReactiveProperty<bool> IsDepositable { get; }

    public DenominationSettingItem(
        DenominationKey key,
        string displayName,
        string displayNameJP,
        int count,
        int nearEmpty,
        int nearFull,
        int full,
        bool isRecyclable,
        bool isDepositable)
    {
        Key = key;
        DisplayName = new BindableReactiveProperty<string>(displayName).AddTo(_disposables);
        DisplayNameJP = new BindableReactiveProperty<string>(displayNameJP).AddTo(_disposables);
        Count = new BindableReactiveProperty<int>(count).AddTo(_disposables);
        NearEmpty = new BindableReactiveProperty<int>(nearEmpty).AddTo(_disposables);
        NearFull = new BindableReactiveProperty<int>(nearFull).AddTo(_disposables);
        Full = new BindableReactiveProperty<int>(full).AddTo(_disposables);
        IsRecyclable = new BindableReactiveProperty<bool>(isRecyclable).AddTo(_disposables);
        IsDepositable = new BindableReactiveProperty<bool>(isDepositable).AddTo(_disposables);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
