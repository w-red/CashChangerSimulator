using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using R3;
using System.Text.Json;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;

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

    public BindableReactiveProperty<bool> IsRealTimeDataEnabled { get; }
    public BindableReactiveProperty<string> ScriptInput { get; }
    public BindableReactiveProperty<string?> ScriptError { get; }
    public ReactiveCommand<Unit> ExecuteScriptCommand { get; }
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    public BindableReactiveProperty<decimal> CurrentDepositAmount { get; }
    public BindableReactiveProperty<bool> IsDepositInProgress { get; }

    public BindableReactiveProperty<bool> IsJammed { get; }
    public BindableReactiveProperty<bool> IsOverlapped { get; }
    public BindableReactiveProperty<bool> IsDeviceError { get; }

    public ReactiveCommand<Unit> ResetErrorCommand { get; }
    public ReactiveCommand<Unit> SimulateJamCommand { get; }
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }
    public ReactiveCommand<Unit> SimulateDeviceErrorCommand { get; }

    /// <summary>必要なコンポーネントを注入して AdvancedSimulationViewModel を初期化します。</summary>
    /// <param name="cashChanger">対象の <see cref="SimulatorCashChanger"/>。</param>
    /// <param name="scriptExecutionService">スクリプト実行サービス。</param>
    /// <param name="depositController">入金制御コントローラー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    public AdvancedSimulationViewModel(
        SimulatorCashChanger cashChanger,
        IScriptExecutionService scriptExecutionService,
        DepositController depositController,
        CurrencyMetadataProvider metadataProvider)
    {
        _cashChanger = cashChanger;
        _scriptExecutionService = scriptExecutionService;

        CurrencyPrefix = metadataProvider.SymbolPrefix.ToReadOnlyReactiveProperty().AddTo(_disposables);
        CurrencySuffix = metadataProvider.SymbolSuffix.ToReadOnlyReactiveProperty().AddTo(_disposables);

        IsRealTimeDataEnabled = new BindableReactiveProperty<bool>(_cashChanger.RealTimeDataEnabled).AddTo(_disposables);
        IsRealTimeDataEnabled
            .Subscribe(enabled => _cashChanger.RealTimeDataEnabled = enabled)
            .AddTo(_disposables);

        CurrentDepositAmount = depositController.Changed
            .Select(_ => depositController.DepositAmount)
            .ToBindableReactiveProperty(depositController.DepositAmount)
            .AddTo(_disposables);

        IsDepositInProgress = depositController.Changed
            .Select(_ => depositController.IsDepositInProgress)
            .ToBindableReactiveProperty(depositController.IsDepositInProgress)
            .AddTo(_disposables);

        IsJammed = _cashChanger.HardwareStatus.IsJammed.ToBindableReactiveProperty().AddTo(_disposables);
        IsOverlapped = _cashChanger.HardwareStatus.IsOverlapped.ToBindableReactiveProperty().AddTo(_disposables);
        IsDeviceError = _cashChanger.HardwareStatus.IsDeviceError.ToBindableReactiveProperty().AddTo(_disposables);

        ResetErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _cashChanger.HardwareStatus.ResetError());

        SimulateJamCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetJammed(true));

        SimulateOverlapCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetOverlapped(true));

        SimulateDeviceErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateDeviceErrorCommand.Subscribe(_ => _cashChanger.HardwareStatus.SetDeviceError(999, 0));

        ScriptInput = new BindableReactiveProperty<string>("[\n  {\n    \"Op\": \"BeginDeposit\"\n  }\n]").AddTo(_disposables);
        ScriptError = new BindableReactiveProperty<string?>(null).AddTo(_disposables);

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

    public void Dispose()
    {
        // Explicitly disable event generation before disposal to prevent SDK exceptions
        _cashChanger.RealTimeDataEnabled = false;
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
