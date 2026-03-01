using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using R3;
using System.Text.Json;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 高度なシミュレーション機能（UPOS 準拠、スクリプト実行等）を管理する ViewModel。
/// </summary>
public class AdvancedSimulationViewModel : IDisposable
{
    private readonly SimulatorCashChanger _cashChanger;
    private readonly IScriptExecutionService _scriptExecutionService;
    private readonly CompositeDisposable _disposables = new();

    public BindableReactiveProperty<bool> IsRealTimeDataEnabled { get; }
    public BindableReactiveProperty<string> ScriptInput { get; }
    public BindableReactiveProperty<string?> ScriptError { get; }
    public ReactiveCommand<Unit> ExecuteScriptCommand { get; }
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }
    
    public BindableReactiveProperty<decimal> CurrentDepositAmount { get; }
    public BindableReactiveProperty<bool> IsDepositInProgress { get; }

    public AdvancedSimulationViewModel(SimulatorCashChanger cashChanger, IScriptExecutionService scriptExecutionService, DepositController depositController, CurrencyMetadataProvider metadataProvider)
    {
        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        _cashChanger = cashChanger;
        _scriptExecutionService = scriptExecutionService;

        IsRealTimeDataEnabled = new BindableReactiveProperty<bool>(_cashChanger.RealTimeDataEnabled);
        
        CurrentDepositAmount = depositController.Changed
            .Select(_ => depositController.DepositAmount)
            .ToBindableReactiveProperty(depositController.DepositAmount)
            .AddTo(_disposables);

        IsDepositInProgress = depositController.Changed
            .Select(_ => depositController.IsDepositInProgress)
            .ToBindableReactiveProperty(depositController.IsDepositInProgress)
            .AddTo(_disposables);
        
        IsRealTimeDataEnabled
            .Subscribe(enabled => _cashChanger.RealTimeDataEnabled = enabled)
            .AddTo(_disposables);

        ScriptInput = new BindableReactiveProperty<string>("[\n  {\n    \"Op\": \"BeginDeposit\"\n  }\n]");
        ScriptError = new BindableReactiveProperty<string?>(null);

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
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
