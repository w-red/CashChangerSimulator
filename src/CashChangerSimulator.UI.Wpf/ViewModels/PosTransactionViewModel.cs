using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>POS取引（支払い・お釣り払い出し）の自動フローをシミュレートする ViewModel。</summary>
/// <remarks>
/// 実際の POS レジ業務（Open -> Claim -> Enable -> BeginDeposit -> Fix -> End -> Dispense -> Close）の一連の
/// OPOS シーケンスを自動実行し、その過程をログとして表示します。
/// </remarks>
public class PosTransactionViewModel : IDisposable
{
    private readonly DepositViewModel _deposit;
    private readonly DispenseViewModel _dispense;
    private readonly SimulatorCashChanger _cashChanger;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly INotifyService _notifyService;
    private readonly ILogger<PosTransactionViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];
    private CancellationTokenSource? _timeoutCts;
    private readonly ReactiveProperty<PosTransactionStatus> _status = new(PosTransactionStatus.Idle);

    // --- State Properties ---

    /// <summary>目標金額の入力値。</summary>
    public BindableReactiveProperty<string> TargetAmountInput { get; }

    /// <summary>目標金額（数値型）。</summary>
    public ReadOnlyReactiveProperty<decimal> TargetAmount { get; }

    /// <summary>現在の取引ステータス。</summary>
    public BindableReactiveProperty<PosTransactionStatus> TransactionStatus { get; }

    /// <summary>投入済みの合計金額。</summary>
    public BindableReactiveProperty<decimal> InsertedAmount { get; }

    /// <summary>不足している残り金額。</summary>
    public BindableReactiveProperty<decimal> RemainingAmount { get; }

    /// <summary>お釣りの合計金額。</summary>
    public BindableReactiveProperty<decimal> ChangeAmount { get; }

    /// <summary>取引のタイムアウト時間（秒）。0以下の場合はタイムアウトなし。</summary>
    public BindableReactiveProperty<int> TransactionTimeoutSeconds { get; }

    /// <summary>OPOSアクションのログ。</summary>
    public ObservableCollection<string> OposLog { get; } = [];

    /// <summary>通貨記号。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }

    /// <summary>通貨単位。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>表示用のステータステキスト。</summary>
    public ReadOnlyReactiveProperty<string> StatusText { get; }

    /// <summary>ユーザーへのメッセージテキスト。</summary>
    public ReadOnlyReactiveProperty<string> Message { get; }

    /// <summary>ターゲット金額の合計。</summary>
    public ReadOnlyReactiveProperty<decimal> TotalTargetAmount { get; }

    /// <summary>現在の合計金額。</summary>
    public ReadOnlyReactiveProperty<decimal> CurrentAmount { get; }

    /// <summary>進捗率（0-100）。</summary>
    public ReadOnlyReactiveProperty<double> Progress { get; }

    // --- Commands ---

    /// <summary>取引を開始するコマンド。</summary>
    public ReactiveCommand<Unit> StartCommand { get; }

    /// <summary>取引をキャンセルするコマンド。</summary>
    public ReactiveCommand<Unit> CancelCommand { get; }

    /// <summary>取引完了後にリセットするコマンド。</summary>
    public ReactiveCommand<Unit> ResetCommand { get; }

    /// <summary>現金投入用の金種ボタンリスト。</summary>
    public ObservableCollection<DenominationViewModel> AvailableDenominations { get; }

    /// <summary>金種を投入するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> InsertCashCommand { get; }

    /// <summary>手動：Open/Claim/Enable コマンド。</summary>
    public ReactiveCommand<Unit> ManualOpenCommand { get; }

    /// <summary>手動：BeginDeposit コマンド。</summary>
    public ReactiveCommand<Unit> ManualDepositCommand { get; }

    /// <summary>手動：DispenseChange コマンド。</summary>
    public ReactiveCommand<Unit> ManualDispenseCommand { get; }

    /// <summary>手動：Close コマンド。</summary>
    public ReactiveCommand<Unit> ManualCloseCommand { get; }

    /// <summary>必要なコンポーネントを注入して <see cref="PosTransactionViewModel"/> を初期化します。</summary>
    /// <param name="deposit">入金処理を管理する <see cref="DepositViewModel"/>。</param>
    /// <param name="dispense">出金処理を管理する <see cref="DispenseViewModel"/>。</param>
    /// <param name="cashChanger">シミュレーターのメインデバイスクラス <see cref="SimulatorCashChanger"/>。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理する <see cref="HardwareStatusManager"/>。</param>
    /// <param name="metadataProvider">通貨情報を表す <see cref="CurrencyMetadataProvider"/>。</param>
    /// <param name="getDenominations">利用可能な金種を取得する関数。</param>
    /// <param name="depositController">入金ロジックを制御する <see cref="DepositController"/>。</param>
    /// <param name="notifyService">通知サービス。</param>
    public PosTransactionViewModel(
        DepositViewModel deposit,
        DispenseViewModel dispense,
        SimulatorCashChanger cashChanger,
        HardwareStatusManager hardwareStatusManager,
        CurrencyMetadataProvider metadataProvider,
        Func<IEnumerable<DenominationViewModel>> getDenominations,
        DepositController depositController,
        INotifyService notifyService)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        ArgumentNullException.ThrowIfNull(dispense);
        ArgumentNullException.ThrowIfNull(cashChanger);
        ArgumentNullException.ThrowIfNull(hardwareStatusManager);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(depositController);
        ArgumentNullException.ThrowIfNull(notifyService);

        _deposit = deposit;
        _dispense = dispense;
        _cashChanger = cashChanger;
        _hardwareStatusManager = hardwareStatusManager;
        _notifyService = notifyService;
        _logger = LogProvider.CreateLogger<PosTransactionViewModel>();

        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;

        TargetAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text) ? null :
                !decimal.TryParse(text, out var val) ? new Exception("Invalid amount") :
                val <= 0 ? new Exception("Amount must be positive") :
                val > 100_000_000 ? new Exception("Amount is too large") : null)
            .AddTo(_disposables);

        TargetAmount = TargetAmountInput
            .Select(text => decimal.TryParse(text, out var val) ? val : 0m)
            .ToReadOnlyReactiveProperty(0m)
            .AddTo(_disposables);

        TransactionStatus = _status.ToBindableReactiveProperty().AddTo(_disposables);
        InsertedAmount = _deposit.CurrentDepositAmount;

        RemainingAmount = InsertedAmount
            .CombineLatest(TargetAmountInput, (inserted, targetStr) => decimal.TryParse(targetStr, out var target) ? Math.Max(0, target - inserted) : 0m)
            .ToBindableReactiveProperty(0m)
            .AddTo(_disposables);

        ChangeAmount = InsertedAmount
            .CombineLatest(TargetAmountInput, (inserted, targetStr) => decimal.TryParse(targetStr, out var target) ? Math.Max(0, inserted - target) : 0m)
            .ToBindableReactiveProperty(0m)
            .AddTo(_disposables);

        TransactionTimeoutSeconds = new BindableReactiveProperty<int>(60).AddTo(_disposables);

        // Map existing properties for UI binding
        TotalTargetAmount = TargetAmount;
        CurrentAmount = InsertedAmount.ToReadOnlyReactiveProperty(0m).AddTo(_disposables);

        Progress = TargetAmount
            .CombineLatest(CurrentAmount, (target, current) => target <= 0 ? 0.0 : Math.Min(100.0, (double)(current / target) * 100.0))
            .ToReadOnlyReactiveProperty(0.0)
            .AddTo(_disposables);

        StatusText = _status.Select(s => s switch
        {
            PosTransactionStatus.Idle => "Ready",
            PosTransactionStatus.WaitingForCash => "Waiting for cash...",
            PosTransactionStatus.DispensingChange => "Dispensing change...",
            PosTransactionStatus.Completed => "Completed",
            _ => "Unknown"
        }).ToReadOnlyReactiveProperty("Ready").AddTo(_disposables);

        Message = _status.Select(s => s switch
        {
            PosTransactionStatus.Idle => "Enter target amount to start simulation.",
            PosTransactionStatus.WaitingForCash => "Please insert cash into the terminal.",
            PosTransactionStatus.DispensingChange => "Do not forget your change.",
            PosTransactionStatus.Completed => "Transaction finished successfully.",
            _ => ""
        }).ToReadOnlyReactiveProperty("").AddTo(_disposables);

        // --- Commands Implementation ---

        StartCommand = TargetAmountInput
            .Select(text => !TargetAmountInput.HasErrors && !string.IsNullOrWhiteSpace(text))
            .CombineLatest(_status, (canInput, s) => canInput && s == PosTransactionStatus.Idle)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        StartCommand.Subscribe(_ => StartTransaction());

        CancelCommand = _status
            .Select(s => s == PosTransactionStatus.WaitingForCash)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        CancelCommand.Subscribe(_ => CancelTransaction());

        ManualOpenCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ManualOpenCommand.Subscribe(_ => ExecuteManualOpen());

        ManualDepositCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ManualDepositCommand.Subscribe(_ => ExecuteManualDeposit());

        ManualDispenseCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ManualDispenseCommand.Subscribe(_ => ExecuteManualDispense());

        ManualCloseCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ManualCloseCommand.Subscribe(_ => ExecuteManualClose());

        ResetCommand = _status
            .Select(s => s == PosTransactionStatus.Completed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        ResetCommand.Subscribe(_ =>
        {
            _status.Value = PosTransactionStatus.Idle;
            TargetAmountInput.Value = "";
            OposLog.Clear();
        });

        // Available denominations for cash insertion
        AvailableDenominations = new ObservableCollection<DenominationViewModel>(getDenominations());

        InsertCashCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
        InsertCashCommand.Subscribe(den =>
        {
            if (den != null && _status.Value == PosTransactionStatus.WaitingForCash)
            {
                depositController.TrackDeposit(den.Key);
                LogOpos($"Cash inserted: {den.Name}");
            }
        });

        // --- Automatic Transaction Flow Logic ---

        Observable.CombineLatest(InsertedAmount, _status, (inserted, status) => (inserted, status))
            .Subscribe(async x =>
            {
                var (inserted, status) = x;
                if (status != PosTransactionStatus.WaitingForCash) return;

                // Reset timeout on cash insertion
                ResetTimeout();

                if (decimal.TryParse(TargetAmountInput.Value, out var target) && inserted >= target)
                {
                    await CompleteTransactionAsync();
                }
            }).AddTo(_disposables);
    }

    private void StartTransaction()
    {
        _logger.ZLogInformation($"Starting POS transaction for amount: {TargetAmountInput.Value}");
        LogOpos("--- Sequence Start ---");

        try
        {
            LogOpos("Open()");
            _cashChanger.Open();

            LogOpos("Claim(1000)");
            _cashChanger.Claim(1000);

            LogOpos("DeviceEnabled = true");
            _cashChanger.DeviceEnabled = true;

            LogOpos("DataEventEnabled = true");
            _cashChanger.DataEventEnabled = true;

            LogOpos("BeginDeposit()");
            _cashChanger.BeginDeposit();

            _status.Value = PosTransactionStatus.WaitingForCash;
            ResetTimeout();
        }
        catch (PosControlException pcEx)
        {
            _logger.ZLogError(pcEx, $"Failed to start OPOS sequence: {pcEx.Message}");
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _status.Value = PosTransactionStatus.Idle;
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to start OPOS sequence: {ex.Message}");
            LogOpos($"ERROR: {ex.Message}");
            _status.Value = PosTransactionStatus.Idle;
        }
        _logger.ZLogInformation($"StartTransaction finished. Status: {_status.Value}");
    }

    private void ResetTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();

        var timeoutSec = TransactionTimeoutSeconds.Value;
        if (timeoutSec <= 0) return;

        _timeoutCts = new CancellationTokenSource();
        var token = _timeoutCts.Token;

        Task.Delay(TimeSpan.FromSeconds(timeoutSec), token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && !token.IsCancellationRequested)
            {
                _logger.ZLogWarning($"Transaction timed out after {timeoutSec} seconds.");
                LogOpos($"TIMEOUT: {timeoutSec}s exceeded.");
                CancelTransaction();
            }
        }, TaskScheduler.Default);
    }

    private void StopTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();
        _timeoutCts = null;
    }

    private void LogOpos(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        OposLog.Add($"[{timestamp}] {message}");
    }

    private void CancelTransaction()
    {
        _logger.ZLogInformation($"Canceling POS transaction.");
        StopTimeout();

        try
        {
            LogOpos("FixDeposit()");
            _cashChanger.FixDeposit();
            LogOpos("Cancelling... EndDeposit(Repay)");
            _cashChanger.EndDeposit(CashDepositAction.Repay);
            LogOpos("Release()");
            _cashChanger.Release();
            LogOpos("Close()");
            _cashChanger.Close();
        }
        catch (PosControlException pcEx)
        {
            LogOpos($"POS ERROR [{pcEx.ErrorCode}] during cancel: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            LogOpos($"Error during cancel: {ex.Message}");
        }

        _status.Value = PosTransactionStatus.Idle;
    }

    private async Task CompleteTransactionAsync()
    {
        StopTimeout();
        var inserted = InsertedAmount.Value;
        var targetValue = decimal.TryParse(TargetAmountInput.Value, out var v) ? v : 0m;
        var changeToDispense = (int)Math.Max(0, inserted - targetValue);

        _logger.ZLogInformation($"Amount met. Completing transaction. Inserted: {inserted}, Target: {targetValue}, Change: {changeToDispense}");
        _status.Value = PosTransactionStatus.DispensingChange;

        try
        {
            LogOpos("FixDeposit()");
            _cashChanger.FixDeposit();

            LogOpos("EndDeposit(NoChange)");
            _cashChanger.EndDeposit(CashDepositAction.NoChange);

            if (changeToDispense > 0)
            {
                LogOpos($"DispenseChange({changeToDispense})");
                _cashChanger.DispenseChange(changeToDispense);

                // Wait for dispense to complete
                await Task.Delay(1000);
            }

            LogOpos("DeviceEnabled = false");
            _cashChanger.DeviceEnabled = false;

            LogOpos("Release()");
            _cashChanger.Release();

            LogOpos("Close()");
            _cashChanger.Close();

            LogOpos("--- Sequence Completed ---");
        }
        catch (PosControlException pcEx)
        {
            _logger.ZLogError(pcEx, $"Failed to complete OPOS sequence: {pcEx.Message}");
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to complete OPOS sequence: {ex.Message}");
            LogOpos($"ERROR: {ex.Message}");
        }

        _status.Value = PosTransactionStatus.Completed;
        await Task.Delay(3000); // Show "Completed" for a while

        _status.Value = PosTransactionStatus.Idle;
        TargetAmountInput.Value = "";
    }

    private void ExecuteManualOpen()
    {
        LogOpos("--- Manual Open ---");
        try
        {
            _cashChanger.Open();
            LogOpos("Open()");
            _cashChanger.Claim(1000);
            LogOpos("Claim(1000)");
            _cashChanger.DeviceEnabled = true;
            LogOpos("DeviceEnabled = true");
            _cashChanger.DataEventEnabled = true;
            LogOpos("DataEventEnabled = true");
        }
        catch (PosControlException pcEx)
        {
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
        catch (Exception ex)
        {
            LogOpos($"ERROR: {ex.Message}");
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }

    private void ExecuteManualDeposit()
    {
        LogOpos("--- Manual Deposit ---");
        try
        {
            _cashChanger.BeginDeposit();
            LogOpos("BeginDeposit()");
            _status.Value = PosTransactionStatus.WaitingForCash;
        }
        catch (PosControlException pcEx)
        {
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            LogOpos($"ERROR: {ex.Message}");
        }
    }

    private void ExecuteManualDispense()
    {
        LogOpos("--- Manual Dispense ---");
        try
        {
            _cashChanger.FixDeposit();
            LogOpos("FixDeposit()");
            _cashChanger.EndDeposit(CashDepositAction.NoChange);
            LogOpos("EndDeposit(NoChange)");

            var inserted = InsertedAmount.Value;
            var targetValue = decimal.TryParse(TargetAmountInput.Value, out var v) ? v : 0m;
            var changeToDispense = (int)Math.Max(0, inserted - targetValue);

            if (changeToDispense > 0)
            {
                _cashChanger.DispenseChange(changeToDispense);
                LogOpos($"DispenseChange({changeToDispense})");
            }
        }
        catch (PosControlException pcEx)
        {
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            LogOpos($"ERROR: {ex.Message}");
        }
    }

    private void ExecuteManualClose()
    {
        LogOpos("--- Manual Close ---");
        try
        {
            _cashChanger.DeviceEnabled = false;
            LogOpos("DeviceEnabled = false");
            _cashChanger.Release();
            LogOpos("Release()");
            _cashChanger.Close();
            LogOpos("Close()");
            _status.Value = PosTransactionStatus.Idle;
        }
        catch (PosControlException pcEx)
        {
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            LogOpos($"ERROR: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        _timeoutCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
