using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
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
    private readonly IDeviceFacade _facade;
    private readonly IScriptExecutionService _scriptExecutionService;
    private readonly IInventoryOperationService _inventoryOperationService;
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

    /// <summary>何らかのエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsAnyError { get; }

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

    /// <summary>スクリプト入力をクリアするコマンド。</summary>
    public ReactiveCommand<Unit> ClearScriptInputCommand { get; }

    /// <summary>ウィンドウを閉じるためのイベント通知。</summary>
    public ReactiveCommand<Unit> CloseCommand { get; }

    /// <summary>依存関係を注入して <see cref="AdvancedSimulationViewModel"/> を初期化します。</summary>
    /// <param name="facade">デバイスとコア機能の Facade。</param>
    /// <param name="scriptExecutionService">スクリプト実行を担う <see cref="IScriptExecutionService"/>。</param>
    /// <param name="metadataProvider">通貨情報を表す <see cref="CurrencyMetadataProvider"/>。</param>
    public AdvancedSimulationViewModel(
        IDeviceFacade facade,
        IScriptExecutionService scriptExecutionService,
        IInventoryOperationService inventoryOperationService,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(scriptExecutionService);
        ArgumentNullException.ThrowIfNull(inventoryOperationService);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _facade = facade;
        _scriptExecutionService = scriptExecutionService;
        _inventoryOperationService = inventoryOperationService;

        ScriptInput = new BindableReactiveProperty<string>("[\n  {\n    \"Op\": \"BeginDeposit\"\n  }\n]").AddTo(_disposables);
        ScriptError = new BindableReactiveProperty<string?>(null).AddTo(_disposables);

        CurrencyPrefix = metadataProvider.SymbolPrefix.ToReadOnlyReactiveProperty().AddTo(_disposables);
        CurrencySuffix = metadataProvider.SymbolSuffix.ToReadOnlyReactiveProperty().AddTo(_disposables);

        CurrentDepositAmount = facade.Deposit.Changed
                .Select(_ => facade.Deposit.DepositAmount)
                .ToBindableReactiveProperty(facade.Deposit.DepositAmount)
                .AddTo(_disposables);

        IsDepositInProgress = facade.Deposit.Changed
                .Select(_ => facade.Deposit.IsDepositInProgress)
                .ToBindableReactiveProperty(facade.Deposit.IsDepositInProgress)
                .AddTo(_disposables);

        IsJammed = facade.Status.IsJammed.ToBindableReactiveProperty().AddTo(_disposables);
        IsOverlapped = facade.Status.IsOverlapped.ToBindableReactiveProperty().AddTo(_disposables);
        IsDeviceError = facade.Status.IsDeviceError.ToBindableReactiveProperty().AddTo(_disposables);

        IsAnyError = Observable.CombineLatest(IsJammed, IsOverlapped, IsDeviceError, (j, o, e) => j || o || e)
                .ToReadOnlyReactiveProperty()
                .AddTo(_disposables);

        ResetErrorCommand = IsAnyError.ToReactiveCommand().AddTo(_disposables);
        SimulateJamCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateOverlapCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateDeviceErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ClearScriptInputCommand = new ReactiveCommand<Unit>().AddTo(_disposables);

        IsRealTimeDataEnabled = new BindableReactiveProperty<bool>(facade.Changer.RealTimeDataEnabled).AddTo(_disposables);
        
        IsRealTimeDataEnabled
            .Skip(1) // Skip initial sync during construction
            .Subscribe(enabled => {
                try { _facade.Changer.RealTimeDataEnabled = enabled; }
                catch (Exception ex) { ScriptError.Value = $"RTD Error: {ex.Message}"; }
            })
            .AddTo(_disposables);

        ResetErrorCommand.Subscribe(_ => _inventoryOperationService.ResetError());
        SimulateJamCommand.Subscribe(_ => _inventoryOperationService.SimulateJam());
        SimulateOverlapCommand.Subscribe(_ => _inventoryOperationService.SimulateOverlap());
        SimulateDeviceErrorCommand.Subscribe(_ => _facade.Status.SetDeviceError(999, 0));
        ClearScriptInputCommand.Subscribe(_ => ScriptInput.Value = string.Empty);
        CloseCommand = new ReactiveCommand<Unit>().AddTo(_disposables);

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
        _facade.Changer.RealTimeDataEnabled = false;
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
