using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>設定画面の ViewModel。閾値と初期枚数の編集・保存・バリデーションを担当する。</summary>
public class SettingsViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly Services.CurrencyMetadataProvider _metadataProvider;
    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>プロパティ値が変更されたときに発生するイベント。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>バリデーションエラーが変更されたときに発生するイベント。</summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>選択されている通貨コード。</summary>
    private string _currencyCode = "JPY";
    public string CurrencyCode
    {
        get => _currencyCode;
        set { _currencyCode = value; OnPropertyChanged(); }
    }

    /// <summary>利用可能な通貨コードのリスト。</summary>
    public string[] AvailableCurrencyCodes { get; } = { "JPY", "USD" };

    /// <summary>NearEmpty と判定するデフォルト枚数。</summary>
    private int _nearEmpty;
    public int NearEmpty
    {
        get => _nearEmpty;
        set { _nearEmpty = value; OnPropertyChanged(); Validate(); }
    }

    /// <summary>NearFull と判定するデフォルト枚数。</summary>
    private int _nearFull;
    public int NearFull
    {
        get => _nearFull;
        set { _nearFull = value; OnPropertyChanged(); Validate(); }
    }

    /// <summary>Full と判定するデフォルト枚数。</summary>
    private int _full;
    public int Full
    {
        get => _full;
        set { _full = value; OnPropertyChanged(); Validate(); }
    }

    /// <summary>各金種の詳細設定リスト。</summary>
    public ObservableCollection<DenominationSettingItem> DenominationSettings { get; } = new();

    /// <summary>設定を保存するコマンド。</summary>
    public ICommand SaveCommand { get; }
    /// <summary>設定をデフォルト値にリセットするコマンド。</summary>
    public ICommand ResetToDefaultCommand { get; }

    /// <summary>エラーが存在するかどうかを取得する。</summary>
    public bool HasErrors => _errors.Any(e => e.Value.Count > 0);
    /// <summary>指定したプロパティのエラー一覧を取得する。</summary>
    public System.Collections.IEnumerable GetErrors(string? propertyName)
    {
        if (propertyName != null && _errors.TryGetValue(propertyName, out var errors))
            return errors;
        return Array.Empty<string>();
    }

    /// <summary>直前の保存処理が成功したかどうか。</summary>
    public bool SaveSucceeded { get; private set; }

    /// <summary>各プロバイダーを注入してインスタンスを初期化する。</summary>
    public SettingsViewModel(ConfigurationProvider configProvider, MonitorsProvider monitorsProvider, Services.CurrencyMetadataProvider metadataProvider)
    {
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;

        LoadFromConfig(configProvider.Config);

        SaveCommand = new RelayCommand(Save, () => !HasErrors);
        ResetToDefaultCommand = new RelayCommand(ResetToDefault);
    }

    /// <summary>設定オブジェクトから ViewModel の各プロパティへ値を読み込む。</summary>
    private void LoadFromConfig(SimulatorConfiguration config)
    {
        CurrencyCode = config.CurrencyCode;
        NearEmpty = config.Thresholds.NearEmpty;
        NearFull = config.Thresholds.NearFull;
        Full = config.Thresholds.Full;

        DenominationSettings.Clear();
        
        foreach (var key in _metadataProvider.SupportedDenominations)
        {
            var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
            
            // 個別設定がある場合はそれを使用、ない場合はデフォルト値を適用
            if (config.Inventory.Denominations.TryGetValue(keyStr, out var setting))
            {
                DenominationSettings.Add(new DenominationSettingItem(
                    key, 
                    setting.DisplayName ?? _metadataProvider.GetDenominationName(key),
                    setting.InitialCount,
                    setting.NearEmpty,
                    setting.NearFull,
                    setting.Full));
            }
            else
            {
                // 互換性維持: 旧 InitialCounts に値があるか確認
                int count = config.Inventory.InitialCounts.TryGetValue(keyStr, out var c) ? c : 0;
                
                DenominationSettings.Add(new DenominationSettingItem(
                    key, 
                    _metadataProvider.GetDenominationName(key),
                    count,
                    NearEmpty,
                    NearFull,
                    Full));
            }
        }
    }

    /// <summary>現在の入力値を設定ファイルへ書き出し、保存とホットリロードを実行する。</summary>
    private void Save()
    {
        var config = _configProvider.Config;
        config.CurrencyCode = CurrencyCode;
        config.Thresholds.NearEmpty = NearEmpty;
        config.Thresholds.NearFull = NearFull;
        config.Thresholds.Full = Full;

        config.Inventory.Denominations.Clear();
        foreach (var item in DenominationSettings)
        {
            var keyStr = (item.Key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + item.Key.Value.ToString();
            config.Inventory.Denominations[keyStr] = new DenominationSettings
            {
                DisplayName = item.DisplayName,
                InitialCount = item.Count,
                NearEmpty = item.NearEmpty,
                NearFull = item.NearFull,
                Full = item.Full
            };
        }

        ConfigurationLoader.Save(config);
        _configProvider.Reload();

        // ホットリロード: 全モニターの閾値を構成に従って即時更新
        _monitorsProvider.UpdateThresholdsFromConfig(config);

        SaveSucceeded = true;
    }

    /// <summary>設定をデフォルト値（初期値）に戻す。</summary>
    private void ResetToDefault()
    {
        var defaultConfig = new SimulatorConfiguration();
        defaultConfig.CurrencyCode = CurrencyCode; 
        LoadFromConfig(defaultConfig);
    }

    /// <summary>入力値の整合性を検証する。</summary>
    private void Validate()
    {
        ClearErrors(nameof(NearEmpty));
        ClearErrors(nameof(NearFull));
        ClearErrors(nameof(Full));

        if (NearEmpty <= 0)
            AddError(nameof(NearEmpty), "NearEmpty は 1 以上にしてください。");
        if (NearFull <= NearEmpty)
            AddError(nameof(NearFull), "NearFull は NearEmpty より大きくしてください。");
        if (Full <= NearFull)
            AddError(nameof(Full), "Full は NearFull より大きくしてください。");

        OnPropertyChanged(nameof(HasErrors));
    }

    /// <summary>エラー情報を追加し、通知を発生させる。</summary>
    private void AddError(string propertyName, string error)
    {
        if (!_errors.ContainsKey(propertyName))
            _errors[propertyName] = new List<string>();
        _errors[propertyName].Add(error);
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    /// <summary>特定プロパティのエラー情報を消去し、通知を発生させる。</summary>
    private void ClearErrors(string propertyName)
    {
        if (_errors.Remove(propertyName))
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }

    /// <summary>プロパティ変更通知を発生させる。</summary>
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>金種ごとの詳細設定を保持・管理するデータ項目。</summary>
public class DenominationSettingItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public DenominationKey Key { get; }
    
    private string _displayName;
    public string DisplayName {
        get => _displayName;
        set { _displayName = value; OnPropertyChanged(); }
    }

    private int _count;
    public int Count {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }

    private int _nearEmpty;
    public int NearEmpty {
        get => _nearEmpty;
        set { _nearEmpty = value; OnPropertyChanged(); }
    }

    private int _nearFull;
    public int NearFull {
        get => _nearFull;
        set { _nearFull = value; OnPropertyChanged(); }
    }

    private int _full;
    public int Full {
        get => _full;
        set { _full = value; OnPropertyChanged(); }
    }

    public DenominationSettingItem(DenominationKey key, string displayName, int count, int nearEmpty, int nearFull, int full)
    {
        Key = key;
        _displayName = displayName;
        _count = count;
        _nearEmpty = nearEmpty;
        _nearFull = nearFull;
        _full = full;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>アクションを実行するための ICommand 実装クラス。</summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>コマンドの実行可能性が変更されたときに発生するイベント。</summary>
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <summary>実行ロジックと実行可能条件を指定して初期化する。</summary>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>コマンドが実行可能かどうかを判断する。</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    /// <summary>コマンドを実行する。</summary>
    public void Execute(object? parameter) => _execute();
}
