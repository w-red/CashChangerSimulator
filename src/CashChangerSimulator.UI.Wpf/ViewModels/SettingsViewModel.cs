using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using R3;
using System.Collections.ObjectModel;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>シミュレータの各種設定を編集・保存するための ViewModel。</summary>
/// <remarks>
/// 通貨コード、UI モード、各種センサーのしきい値、および金種ごとの個別設定の管理を担当します。
/// 設定のバリデーション、ディスクへの保存、および実行時への変更反映（ホットリロード）のトリガーを提供します。
/// </remarks>
public class SettingsViewModel : IDisposable
{
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
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

    /// <summary>表示言語（カルチャコード）。</summary>
    public BindableReactiveProperty<string> CultureCode { get; }

    /// <summary>利用可能な言語のリスト。</summary>
    public string[] AvailableCultures { get; } = ["en-US", "ja-JP"];

    /// <summary>NearEmpty と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearEmpty { get; }

    /// <summary>NearFull と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> NearFull { get; }

    /// <summary>Full と判定するデフォルト枚数。</summary>
    public BindableReactiveProperty<int> Full { get; }

    /// <summary>起動時に自動オープンするかどうか (Hot Start)。</summary>
    public BindableReactiveProperty<bool> HotStart { get; }


    /// <summary>各金種の詳細設定リスト。</summary>
    public ObservableCollection<DenominationSettingItem> DenominationSettings { get; } = [];

    /// <summary>設定を保存するコマンド。</summary>
    public ReactiveCommand SaveCommand { get; }

    /// <summary>設定をデフォルト値にリセットするコマンド。</summary>
    public ReactiveCommand ResetToDefaultCommand { get; }

    /// <summary>直前の保存処理が成功したかどうか。</summary>
    public BindableReactiveProperty<bool> SaveSucceeded { get; }

    /// <summary>必要なコンポーネントを注入して SettingsViewModel を初期化します。</summary>
    /// <remarks>現在の設定値のロードと、バリデーションロジックの構成を行います。</remarks>
    public SettingsViewModel(
        ConfigurationProvider configProvider,
        MonitorsProvider monitorsProvider,
        CurrencyMetadataProvider metadataProvider)
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
        CultureCode =
            new BindableReactiveProperty<string>("en-US")
            .AddTo(_disposables);

        CultureCode.Subscribe(App.UpdateLanguage).AddTo(_disposables);

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

        HotStart = new BindableReactiveProperty<bool>(false)
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
        CurrencyCode.Value = config.System.CurrencyCode;
        NearEmpty.Value = config.Thresholds.NearEmpty;
        NearFull.Value = config.Thresholds.NearFull;
        Full.Value = config.Thresholds.Full;
        ActiveUIMode.Value = config.System.UIMode;
        CultureCode.Value = config.System.CultureCode ?? "en-US";
        HotStart.Value = config.Simulation.HotStart;

        DenominationSettings.Clear();

        foreach (var key in _metadataProvider.SupportedDenominations)
        {
            var keyStr = (key.Type == CurrencyCashType.Bill ? "B" : "C") + key.Value.ToString();

            // 個別設定がある場合はそれを使用、ない場合はデフォルト値を適用
            if (config.Inventory.TryGetValue(config.System.CurrencyCode, out var inventory) &&
                inventory.Denominations.TryGetValue(keyStr, out var setting))
            {
                DenominationSettings.Add(new DenominationSettingItem(
                    key,
                    setting.DisplayName ?? _metadataProvider.GetDenominationName(key, "en-US"),
                    setting.DisplayNameJP ?? _metadataProvider.GetDenominationName(key, "ja-JP"),
                    setting.InitialCount,
                    setting.NearEmpty,
                    setting.NearFull,
                    setting.Full,
                    setting.IsRecyclable,
                    setting.IsDepositable));
            }
            else
            {
                DenominationSettings.Add(new DenominationSettingItem(
                    key,
                    _metadataProvider.GetDenominationName(key, "en-US"),
                    _metadataProvider.GetDenominationName(key, "ja-JP"),
                    0,
                    NearEmpty.Value,
                    NearFull.Value,
                    Full.Value,
                    true,
                    true));
            }
        }
    }

    /// <summary>現在の入力値を設定ファイルへ書き出し、保存とホットリロードを実行する。</summary>
    private void Save()
    {
        var config = _configProvider.Config;
        config.System.CurrencyCode = CurrencyCode.Value;
        config.Thresholds.NearEmpty = NearEmpty.Value;
        config.Thresholds.NearFull = NearFull.Value;
        config.Thresholds.Full = Full.Value;

        config.System.UIMode = ActiveUIMode.Value;
        config.System.CultureCode = CultureCode.Value;
        config.Simulation.HotStart = HotStart.Value;

        if (!config.Inventory.TryGetValue(config.System.CurrencyCode, out InventorySettings? activeInventory))
        {
            activeInventory = new InventorySettings();
            config.Inventory[config.System.CurrencyCode] = activeInventory;
        }

        activeInventory.Denominations.Clear();
        foreach (var item in DenominationSettings)
        {
            var keyStr = (item.Key.Type == CurrencyCashType.Bill ? "B" : "C") + item.Key.Value.ToString();
            activeInventory.Denominations[keyStr] = new DenominationSettings
            {
                DisplayName = item.DisplayName.Value,
                DisplayNameJP = item.DisplayNameJP.Value,
                InitialCount = item.Count.Value,
                NearEmpty = item.NearEmpty.Value,
                NearFull = item.NearFull.Value,
                Full = item.Full.Value,
                IsRecyclable = item.IsRecyclable.Value,
                IsDepositable = item.IsDepositable.Value
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
        var defaultConfig = new SimulatorConfiguration();
        defaultConfig.System.CurrencyCode = CurrencyCode.Value;
        LoadFromConfig(defaultConfig);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}

