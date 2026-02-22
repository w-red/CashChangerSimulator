using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using Microsoft.Extensions.Logging;
using R3;
using System.Collections.ObjectModel;
using ZLogger;

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

    /// <summary>利用可能な UI モードのリスト。</summary>
    public UIMode[] AvailableUIModes { get; } = [UIMode.Standard, UIMode.PosTransaction];

    /// <summary>NearEmpty と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }

    /// <summary>NearFull と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearFull { get; }

    /// <summary>Full と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> Full { get; }


    /// <summary>各金種の詳細設定リスト。</summary>
    public ObservableCollection<DenominationSettingItem> DenominationSettings { get; } = [];

    /// <summary>設定を保存するコマンド。</summary>
    public ReactiveCommand SaveCommand { get; }

    /// <summary>設定をデフォルト値にリセットするコマンド。</summary>
    public ReactiveCommand ResetToDefaultCommand { get; }

    /// <summary>直前の保存処理が成功したかどうか。</summary>
    public BindableReactiveProperty<bool> SaveSucceeded { get; }

    /// <summary>各プロバイダーを注入してインスタンスを初期化する。</summary>
    public SettingsViewModel(
        ConfigurationProvider configProvider,
        MonitorsProvider monitorsProvider,
        Services.CurrencyMetadataProvider metadataProvider)
    {
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _logger = LogProvider.CreateLogger<SettingsViewModel>();

        CurrencyCode =
            new BindableReactiveProperty<string>("JPY")
            .AddTo(_disposables);
        ActiveUIMode =
            new BindableReactiveProperty<UIMode>(UIMode.Standard)
            .AddTo(_disposables);

        NearEmpty = new BindableReactiveProperty<int>(0)
            .EnableValidation(val =>
                val <= 0
                ? new Exception("NearEmpty は 1 以上にしてください。")
                : null)
            .AddTo(_disposables);

        NearFull = new BindableReactiveProperty<int>(0)
            .EnableValidation(val =>
                val <= NearEmpty.Value
                ? new Exception("NearFull は NearEmpty より大きくしてください。")
                : null)
            .AddTo(_disposables);

        Full = new BindableReactiveProperty<int>(0)
            .EnableValidation(val =>
                val <= NearFull.Value
                ? new Exception("Full は NearFull より大きくしてください。")
                : null)
            .AddTo(_disposables);


        SaveSucceeded =
            new BindableReactiveProperty<bool>(false)
            .AddTo(_disposables);

        LoadFromConfig(configProvider.Config);

        var canSave = Observable.CombineLatest(
            NearEmpty, NearFull, Full,
            (_, _, _) => 
                !NearEmpty.HasErrors &&
                !NearFull.HasErrors &&
                !Full.HasErrors);

        SaveCommand = canSave
            .ToReactiveCommand()
            .AddTo(_disposables);
        SaveCommand
            .Subscribe(_ => Save());

        ResetToDefaultCommand =
            new ReactiveCommand()
            .AddTo(_disposables);
        ResetToDefaultCommand
            .Subscribe(_ => ResetToDefault());
    }

    /// <summary>設定オブジェクトから ViewModel の各プロパティへ値を読み込む。</summary>
    private void LoadFromConfig(SimulatorConfiguration config)
    {
        CurrencyCode.Value = config.CurrencyCode;
        NearEmpty.Value = config.Thresholds.NearEmpty;
        NearFull.Value = config.Thresholds.NearFull;
        Full.Value = config.Thresholds.Full;
        ActiveUIMode.Value = config.UIMode;

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

        config.UIMode = ActiveUIMode.Value;

        if (!config.Inventory.TryGetValue(config.CurrencyCode, out InventorySettings? activeInventory))
        {
            activeInventory = new InventorySettings();
            config.Inventory[config.CurrencyCode] = activeInventory;
        }

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

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
