using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
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
    private readonly IDeviceFacade _facade;
    private readonly INotifyService _notifyService;
    private readonly ILogger<PosTransactionViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];
    private CancellationTokenSource? _timeoutCts;
    private readonly ReactiveProperty<PosTransactionStatus> _status = new(PosTransactionStatus.Idle);
    private bool _disposed;

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
    /// <param name="facade">デバイスとコア機能の Facade。</param>
    /// <param name="deposit">入金処理を管理する <see cref="DepositViewModel"/>。</param>
    /// <param name="dispense">出金処理を管理する <see cref="DispenseViewModel"/>。</param>
    /// <param name="metadataProvider">通貨情報を表す <see cref="CurrencyMetadataProvider"/>。</param>
    /// <param name="getDenominations">利用可能な金種を取得する関数。</param>
    /// <param name="notifyService">通知サービス。</param>
    public PosTransactionViewModel(
        IDeviceFacade facade,
        DepositViewModel deposit,
        DispenseViewModel dispense,
        CurrencyMetadataProvider metadataProvider,
        Func<IEnumerable<DenominationViewModel>> getDenominations,
        INotifyService notifyService)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(deposit);
        ArgumentNullException.ThrowIfNull(dispense);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(notifyService);

        _facade = facade;
        _deposit = deposit;
        _dispense = dispense;
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
                _facade.Deposit.TrackDeposit(den.Key);
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

        ExecuteOposAction(() =>
        {
            InitializeDeviceSequence();
            LogOpos("BeginDeposit()");
            _facade.Changer.BeginDeposit();

            _status.Value = PosTransactionStatus.WaitingForCash;
            ResetTimeout();
        }, "start transaction", onException: () => _status.Value = PosTransactionStatus.Idle);

        _logger.ZLogInformation($"StartTransaction finished. Status: {_status.Value}");
    }

    private void ResetTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();

        var timeoutSec = TransactionTimeoutSeconds.Value;
        if (timeoutSec <= 0 || _disposed) return;

        _timeoutCts = new CancellationTokenSource();
        var token = _timeoutCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(timeoutSec), token);
                if (!token.IsCancellationRequested && !_disposed)
                {
                    _logger.ZLogWarning($"Transaction timed out after {timeoutSec} seconds.");
                    LogOpos($"TIMEOUT: {timeoutSec}s exceeded.");
                    CancelTransaction();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.ZLogError($"Unhandled exception in transaction timeout task: {ex.Message}");
            }
        }, token);
    }

    private void StopTimeout()
    {
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();
        _timeoutCts = null;
    }

    private void LogOpos(string message)
    {
        if (_disposed) return;
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        OposLog.Add($"[{timestamp}] {message}");
    }

    private void CancelTransaction()
    {
        if (_disposed) return;
        _logger.ZLogInformation($"Canceling POS transaction.");
        StopTimeout();

        ExecuteOposAction(() =>
        {
            LogOpos("FixDeposit()");
            _facade.Changer.FixDeposit();
            LogOpos("Cancelling... EndDeposit(Repay)");
            _facade.Changer.EndDeposit(CashDepositAction.Repay);
            FinalizeDeviceSequence();
        }, "cancel transaction");

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

        await ExecuteOposActionAsync(async () =>
        {
            LogOpos("FixDeposit()");
            _facade.Changer.FixDeposit();

            LogOpos("EndDeposit(NoChange)");
            _facade.Changer.EndDeposit(CashDepositAction.NoChange);

            if (changeToDispense > 0)
            {
                LogOpos($"DispenseChange({changeToDispense})");
                _facade.Changer.DispenseChange(changeToDispense);
                await Task.Delay(PosTransactionConstants.DispenseWaitDelay);
            }

            LogOpos("DeviceEnabled = false");
            _facade.Changer.DeviceEnabled = false;
            FinalizeDeviceSequence();

            LogOpos("--- Sequence Completed ---");
        }, "complete transaction");

        _status.Value = PosTransactionStatus.Completed;
        await Task.Delay(PosTransactionConstants.CompletionResetDelay);

        _status.Value = PosTransactionStatus.Idle;
        TargetAmountInput.Value = "";
    }

    private void ExecuteManualOpen()
    {
        LogOpos("--- Manual Open ---");
        ExecuteOposAction(InitializeDeviceSequence, "manual open", showNotification: true);
    }

    private void ExecuteManualDeposit()
    {
        LogOpos("--- Manual Deposit ---");
        ExecuteOposAction(() =>
        {
            _facade.Changer.BeginDeposit();
            LogOpos("BeginDeposit()");
            _status.Value = PosTransactionStatus.WaitingForCash;
        }, "manual deposit");
    }

    private void ExecuteManualDispense()
    {
        LogOpos("--- Manual Dispense ---");
        ExecuteOposAction(() =>
        {
            LogOpos("FixDeposit()");
            _facade.Changer.FixDeposit();
            LogOpos("EndDeposit(NoChange)");
            _facade.Changer.EndDeposit(CashDepositAction.NoChange);

            var inserted = InsertedAmount.Value;
            var targetValue = decimal.TryParse(TargetAmountInput.Value, out var v) ? v : 0m;
            var changeToDispense = (int)Math.Max(0, inserted - targetValue);

            if (changeToDispense > 0)
            {
                LogOpos($"DispenseChange({changeToDispense})");
                _facade.Changer.DispenseChange(changeToDispense);
            }
        }, "manual dispense");
    }

    private void ExecuteManualClose()
    {
        LogOpos("--- Manual Close ---");
        ExecuteOposAction(() =>
        {
            LogOpos("DeviceEnabled = false");
            _facade.Changer.DeviceEnabled = false;
            FinalizeDeviceSequence();
            _status.Value = PosTransactionStatus.Idle;
        }, "manual close");
    }

    // --- Helpers ---

    private void InitializeDeviceSequence()
    {
        LogOpos("Open()");
        _facade.Changer.Open();

        LogOpos($"Claim({PosTransactionConstants.DefaultClaimTimeout})");
        _facade.Changer.Claim(PosTransactionConstants.DefaultClaimTimeout);

        LogOpos("DeviceEnabled = true");
        _facade.Changer.DeviceEnabled = true;

        LogOpos("DataEventEnabled = true");
        _facade.Changer.DataEventEnabled = true;
    }

    private void FinalizeDeviceSequence()
    {
        LogOpos("Release()");
        _facade.Changer.Release();
        LogOpos("Close()");
        _facade.Changer.Close();
    }

    private void ExecuteOposAction(Action action, string actionName, bool showNotification = false, Action? onException = null)
    {
        try
        {
            action();
        }
        catch (PosControlException pcEx)
        {
            _logger.ZLogError(pcEx, $"Failed to execute OPOS {actionName}: {pcEx.Message}");
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            if (showNotification) _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error", "Error"));
            onException?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to execute OPOS {actionName}: {ex.Message}");
            LogOpos($"ERROR: {ex.Message}");
            if (showNotification) _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
            onException?.Invoke();
        }
    }

    private async Task ExecuteOposActionAsync(Func<Task> action, string actionName)
    {
        try
        {
            await action();
        }
        catch (PosControlException pcEx)
        {
            _logger.ZLogError(pcEx, $"Failed to execute OPOS {actionName}: {pcEx.Message}");
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to execute OPOS {actionName}: {ex.Message}");
            LogOpos($"ERROR: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
        _timeoutCts?.Cancel();
        _timeoutCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
