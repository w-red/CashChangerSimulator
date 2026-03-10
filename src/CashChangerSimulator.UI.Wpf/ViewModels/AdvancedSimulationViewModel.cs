using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using R3;
using System.Text.Json;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>高度なシミュレーション機能（UPOS 準拠、スクリプト実行等）を管理する ViewModel。</summary>
/// <remarks>
/// OPOS の RealTimeDataEnabled プロパティの制御や、JSON スクリプトによる一連の操作の自動実行を担当します。
/// 開発者向けのデバッグ機能や、特定のシーケンスを再現するためのツールとしての役割を持ちます。
/// </remarks>
public class AdvancedSimulationViewModel : IDisposable
{
    private readonly SimulatorCashChanger _cashChanger;
    private readonly IScriptExecutionService _scriptExecutionService;
    private readonly CompositeDisposable _disposables = [];

    // --- State Properties ---

    /// <summary>RealTimeDataEnabled プロパティの現在値。</summary>
    public BindableReactiveProperty<bool> IsRealTimeDataEnabled { get; }

    /// <summary>実行する JSON スクリプトの入力値。</summary>
    public BindableReactiveProperty<string> ScriptInput { get; }

    /// <summary>スクリプトの解析・実行エラーメッセージ。</summary>
    public BindableReactiveProperty<string?> ScriptError { get; }

    /// <summary>通貨記号の接頭辞。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }

    /// <summary>通貨記号の接尾辞。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>現在の入金合計金額。</summary>
    public BindableReactiveProperty<decimal> CurrentDepositAmount { get; }

    /// <summary>現在入金中かどうか。</summary>
    public BindableReactiveProperty<bool> IsDepositInProgress { get; }

    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; }

    /// <summary>重なりが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }

    // --- Commands ---

    /// <summary>スクリプトを実行するコマンド。</summary>
    public ReactiveCommand<Unit> ExecuteScriptCommand { get; }

    /// <summary>エラー状態をリセットするコマンド。</summary>
    public ReactiveCommand<Unit> ResetErrorCommand { get; }

    /// <summary>ジャム状態をシミュレーションするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateJamCommand { get; }

    /// <summary>重なり状態をシミュレーションするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }

    /// <summary>デバイスエラーをシミュレーションするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateDeviceErrorCommand { get; }

    /// <summary>依存関係を注入して <see cref="AdvancedSimulationViewModel"/> を初期化します。</summary>
    /// <param name="cashChanger">シミュレータ本体インスタンス <see cref="SimulatorCashChanger"/>。</param>
    /// <param name="scriptExecutionService">スクリプト実行を担う <see cref="IScriptExecutionService"/>。</param>
    /// <param name="depositController">入金状態を管理する <see cref="DepositController"/>。</param>
    /// <param name="metadataProvider">通貨情報を表す <see cref="CurrencyMetadataProvider"/>。</param>
    public AdvancedSimulationViewModel(
        SimulatorCashChanger cashChanger,
        IScriptExecutionService scriptExecutionService,
        DepositController depositController,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(cashChanger);
        ArgumentNullException.ThrowIfNull(scriptExecutionService);
        ArgumentNullException.ThrowIfNull(depositController);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _cashChanger = cashChanger;
        _scriptExecutionService = scriptExecutionService;

        IsRealTimeDataEnabled = new BindableReactiveProperty<bool>(cashChanger.RealTimeDataEnabled).AddTo(_disposables);
        ScriptInput = new BindableReactiveProperty<string>("[\n  {\n    \"Op\": \"BeginDeposit\"\n  }\n]").AddTo(_disposables);
        ScriptError = new BindableReactiveProperty<string?>(null).AddTo(_disposables);

        CurrencyPrefix = metadataProvider.SymbolPrefix.ToReadOnlyReactiveProperty().AddTo(_disposables);
        CurrencySuffix = metadataProvider.SymbolSuffix.ToReadOnlyReactiveProperty().AddTo(_disposables);

        CurrentDepositAmount = depositController.Changed
                .Select(_ => depositController.DepositAmount)
                .ToBindableReactiveProperty(depositController.DepositAmount)
                .AddTo(_disposables);

        IsDepositInProgress = depositController.Changed
                .Select(_ => depositController.IsDepositInProgress)
                .ToBindableReactiveProperty(depositController.IsDepositInProgress)
                .AddTo(_disposables);

        IsJammed = cashChanger.HardwareStatus.IsJammed.ToBindableReactiveProperty().AddTo(_disposables);
        IsOverlapped = cashChanger.HardwareStatus.IsOverlapped.ToBindableReactiveProperty().AddTo(_disposables);
        IsDeviceError = cashChanger.HardwareStatus.IsDeviceError.ToBindableReactiveProperty().AddTo(_disposables);

        ResetErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateJamCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateOverlapCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateDeviceErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);

        IsRealTimeDataEnabled
            .Subscribe(enabled => _cashChanger.RealTimeDataEnabled = enabled)
            .AddTo(_disposables);

        ResetErrorCommand.Subscribe(_ => _cashChanger.HardwareStatus.ResetError());
        SimulateJamCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetJammed(true));
        SimulateOverlapCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetOverlapped(true));
        SimulateDeviceErrorCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetDeviceError(999, 0));

        // Enables command only if JSON is basically valid list
        var canExecute = ScriptInput.Select(input =>
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            try
            {
                using var document = JsonDocument.Parse(input);
                return document.RootElement.ValueKind == JsonValueKind.Array;
            }
            catch
            {
                return false;
            }
        });

        ExecuteScriptCommand = canExecute.ToReactiveCommand().AddTo(_disposables);
        ExecuteScriptCommand.SubscribeAwait(async (_, ct) =>
        {
            ScriptError.Value = null;
            try
            {
                await _scriptExecutionService.ExecuteScriptAsync(ScriptInput.Value);
            }
            catch (Exception ex)
            {
                ScriptError.Value = $"Execution Error: {ex.Message}";
            }
        });

        // Also subscribe to input to show parsing errors immediately
        ScriptInput.Subscribe(input =>
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                ScriptError.Value = null;
                return;
            }
            try
            {
                JsonDocument.Parse(input);
                ScriptError.Value = null;
            }
            catch (Exception ex)
            {
                ScriptError.Value = $"Parse Error: {ex.Message}";
            }
        }).AddTo(_disposables);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Explicitly disable event generation before disposal to prevent SDK exceptions
        _cashChanger.RealTimeDataEnabled = false;
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
