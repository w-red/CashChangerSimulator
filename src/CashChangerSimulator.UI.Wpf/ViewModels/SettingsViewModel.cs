using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ZLogger;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>設定画面の ViewModel。閾値と初期枚数の編集・保存・バリデーションを担当する。</summary>
public class SettingsViewModel : IDisposable
{
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly Services.CurrencyMetadataProvider _metadataProvider;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>選択されている通貨コード。</summary>
    public BindableReactiveProperty<string> CurrencyCode { get; }

    /// <summary>利用可能な通貨コードのリスト。</summary>
    public string[] AvailableCurrencyCodes { get; } = ["JPY", "USD"];

    /// <summary>シミュレーターのUIモード</summary>
    public BindableReactiveProperty<UIMode> ActiveUIMode { get; }

    public UIMode[] AvailableUIModes { get; } = [UIMode.Standard, UIMode.PosTransaction];

    /// <summary>NearEmpty と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }

    /// <summary>NearFull と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearFull { get; }

    /// <summary>Full と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> Full { get; }

    /// <summary>シミュレーション遅延を有効にするか。</summary>
    public BindableReactiveProperty<bool> UseDelay { get; }

    /// <summary>最小遅延時間 (ms)。</summary>
    public BindableReactiveProperty<int> MinDelay { get; }

    /// <summary>最大遅延時間 (ms)。</summary>
    public BindableReactiveProperty<int> MaxDelay { get; }

    /// <summary>ランダムエラーを有効にするか。</summary>
    public BindableReactiveProperty<bool> UseRandomErrors { get; }

    /// <summary>エラー発生確率 (0-100)。</summary>
    public BindableReactiveProperty<int> ErrorRate { get; }

    /// <summary>各金種の詳細設定リスト。</summary>
    public ObservableCollection<DenominationSettingItem> DenominationSettings { get; } = [];

    /// <summary>設定を保存するコマンド。</summary>
    public ReactiveCommand SaveCommand { get; }
    
    /// <summary>設定をデフォルト値にリセットするコマンド。</summary>
    public ReactiveCommand ResetToDefaultCommand { get; }

    /// <summary>直前の保存処理が成功したかどうか。</summary>
    public BindableReactiveProperty<bool> SaveSucceeded { get; }

    /// <summary>各プロバイダーを注入してインスタンスを初期化する。</summary>
    public SettingsViewModel(ConfigurationProvider configProvider, MonitorsProvider monitorsProvider, Services.CurrencyMetadataProvider metadataProvider)
    {
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _logger = LogProvider.CreateLogger<SettingsViewModel>();

        CurrencyCode = new BindableReactiveProperty<string>("JPY").AddTo(_disposables);
        ActiveUIMode = new BindableReactiveProperty<UIMode>(Core.Configuration.UIMode.Standard).AddTo(_disposables);
        
        NearEmpty = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val <= 0 ? new Exception("NearEmpty は 1 以上にしてください。") : null)
            .AddTo(_disposables);
            
        NearFull = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val <= NearEmpty.Value ? new Exception("NearFull は NearEmpty より大きくしてください。") : null)
            .AddTo(_disposables);
            
        Full = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val <= NearFull.Value ? new Exception("Full は NearFull より大きくしてください。") : null)
            .AddTo(_disposables);

        UseDelay = new BindableReactiveProperty<bool>(false).AddTo(_disposables);
        MinDelay = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val < 0 ? new Exception("MinDelay は 0 以上にしてください。") : null)
            .AddTo(_disposables);
            
        MaxDelay = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val < MinDelay.Value ? new Exception("MaxDelay は MinDelay 以上にしてください。") : null)
            .AddTo(_disposables);

        UseRandomErrors = new BindableReactiveProperty<bool>(false).AddTo(_disposables);
        ErrorRate = new BindableReactiveProperty<int>(0)
            .EnableValidation(val => val < 0 || val > 100 ? new Exception("ErrorRate は 0 から 100 の間にしてください。") : null)
            .AddTo(_disposables);

        SaveSucceeded = new BindableReactiveProperty<bool>(false).AddTo(_disposables);

        LoadFromConfig(configProvider.Config);

        var canSave = Observable.CombineLatest(
            NearEmpty, NearFull, Full,
            MinDelay, MaxDelay, ErrorRate,
            (_, _, _, _, _, _) => !NearEmpty.HasErrors && !NearFull.HasErrors && !Full.HasErrors && !MinDelay.HasErrors && !MaxDelay.HasErrors && !ErrorRate.HasErrors);

        SaveCommand = canSave.ToReactiveCommand().AddTo(_disposables);
        SaveCommand.Subscribe(_ => Save());
        
        ResetToDefaultCommand = new ReactiveCommand().AddTo(_disposables);
        ResetToDefaultCommand.Subscribe(_ => ResetToDefault());
    }

    /// <summary>設定オブジェクトから ViewModel の各プロパティへ値を読み込む。</summary>
    private void LoadFromConfig(SimulatorConfiguration config)
    {
        CurrencyCode.Value = config.CurrencyCode;
        NearEmpty.Value = config.Thresholds.NearEmpty;
        NearFull.Value = config.Thresholds.NearFull;
        Full.Value = config.Thresholds.Full;
        ActiveUIMode.Value = config.Simulation.UIMode;

        UseDelay.Value = config.Simulation.DelayEnabled;
        MinDelay.Value = config.Simulation.MinDelayMs;
        MaxDelay.Value = config.Simulation.MaxDelayMs;
        UseRandomErrors.Value = config.Simulation.RandomErrorsEnabled;
        ErrorRate.Value = config.Simulation.ErrorRate;

        DenominationSettings.Clear();
        
        foreach (var key in _metadataProvider.SupportedDenominations)
        {
            var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
            
            // 個別設定がある場合はそれを使用、ない場合はデフォルト値を適用
            if (config.Inventory.TryGetValue(config.CurrencyCode, out var inventory) &&
                inventory.Denominations.TryGetValue(keyStr, out var setting))
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
                DenominationSettings.Add(new DenominationSettingItem(
                    key, 
                    _metadataProvider.GetDenominationName(key),
                    0,
                    NearEmpty.Value,
                    NearFull.Value,
                    Full.Value));
            }
        }
    }

    /// <summary>現在の入力値を設定ファイルへ書き出し、保存とホットリロードを実行する。</summary>
    private void Save()
    {
        var config = _configProvider.Config;
        config.CurrencyCode = CurrencyCode.Value;
        config.Thresholds.NearEmpty = NearEmpty.Value;
        config.Thresholds.NearFull = NearFull.Value;
        config.Thresholds.Full = Full.Value;

        config.Simulation.DelayEnabled = UseDelay.Value;
        config.Simulation.MinDelayMs = MinDelay.Value;
        config.Simulation.MaxDelayMs = MaxDelay.Value;
        config.Simulation.RandomErrorsEnabled = UseRandomErrors.Value;
        config.Simulation.ErrorRate = ErrorRate.Value;
        config.Simulation.UIMode = ActiveUIMode.Value;

        try
        {
            var simSettings = DIContainer.Resolve<SimulationSettings>();
            simSettings.DelayEnabled = UseDelay.Value;
            simSettings.MinDelayMs = MinDelay.Value;
            simSettings.MaxDelayMs = MaxDelay.Value;
            simSettings.RandomErrorsEnabled = UseRandomErrors.Value;
            simSettings.ErrorRate = ErrorRate.Value;
            simSettings.UIMode = ActiveUIMode.Value;
        }
        catch (Exception ex)
        {
            _logger.ZLogWarning(ex, $"Could not update DI singleton SimulationSettings array. This might require restart to take effect.");
        }

        if (!config.Inventory.ContainsKey(config.CurrencyCode))
        {
            config.Inventory[config.CurrencyCode] = new InventorySettings();
        }
        
        var activeInventory = config.Inventory[config.CurrencyCode];
        activeInventory.Denominations.Clear();
        foreach (var item in DenominationSettings)
        {
            var keyStr = (item.Key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + item.Key.Value.ToString();
            activeInventory.Denominations[keyStr] = new DenominationSettings
            {
                DisplayName = item.DisplayName.Value,
                InitialCount = item.Count.Value,
                NearEmpty = item.NearEmpty.Value,
                NearFull = item.NearFull.Value,
                Full = item.Full.Value
            };
        }

        ConfigurationLoader.Save(config);
        _configProvider.Reload();

        _monitorsProvider.UpdateThresholdsFromConfig(config);

        _logger.ZLogInformation($"Simulator configuration saved and reloaded.");

        SaveSucceeded.Value = true;
    }

    /// <summary>設定をデフォルト値（初期値）に戻す。</summary>
    private void ResetToDefault()
    {
        var defaultConfig = new SimulatorConfiguration
        {
            CurrencyCode = CurrencyCode.Value
        };
        LoadFromConfig(defaultConfig);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>入力値の整合性を検証する。</summary>
    private void Validate()
    {
        // This method is largely redundant as BindableReactiveProperty.EnableValidation() is used.
        // Keeping it for now if there's any other manual validation logic not covered by BRP.
        // If not, this method and related INotifyDataErrorInfo implementations can be removed.
    }
}


