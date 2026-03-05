using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>POS取引（支払い、お釣り払い出し）フローを管理する ViewModel。</summary>
public class PosTransactionViewModel : IDisposable
{
    private readonly DepositViewModel _deposit;
    private readonly DispenseViewModel _dispense;
    private readonly SimulatorCashChanger _cashChanger;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<PosTransactionViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];
    private CancellationTokenSource? _timeoutCts;

    // Properties
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
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    // Missing Binding Properties for UI
    public ReadOnlyReactiveProperty<string> StatusText { get; }
    public ReadOnlyReactiveProperty<string> Message { get; }
    public ReadOnlyReactiveProperty<decimal> TotalTargetAmount { get; }
    public ReadOnlyReactiveProperty<decimal> CurrentAmount { get; }
    public ReadOnlyReactiveProperty<double> Progress { get; }

    // Commands
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

    private readonly ReactiveProperty<PosTransactionStatus> _status = new(PosTransactionStatus.Idle);

    /// <summary>PosTransactionViewModel の新しいインスタンスを初期化します。</summary>
    public PosTransactionViewModel(DepositViewModel deposit, DispenseViewModel dispense, SimulatorCashChanger cashChanger, HardwareStatusManager hardwareStatusManager, CurrencyMetadataProvider metadataProvider, Func<IEnumerable<DenominationViewModel>> getDenominations, DepositController depositController)
    {
        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        _deposit = deposit;
        _dispense = dispense;
        _cashChanger = cashChanger;
        _hardwareStatusManager = hardwareStatusManager;
        _logger = LogProvider.CreateLogger<PosTransactionViewModel>();

        TargetAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text) ? null :
                !decimal.TryParse(text, out var val) ? new Exception("Invalid amount") :
                val <= 0 ? new Exception("Amount must be positive") : 
                val > 100_000_000 ? new Exception("Amount is too large") : null)
            .AddTo(_disposables);

        TargetAmount = TargetAmountInput.Select(text =>
        {
            return decimal.TryParse(text, out var val) ? val : 0m;
        }).ToReadOnlyReactiveProperty(0m).AddTo(_disposables);

        TransactionStatus = _status.ToBindableReactiveProperty().AddTo(_disposables);

        InsertedAmount = _deposit.CurrentDepositAmount;

        RemainingAmount = InsertedAmount.CombineLatest(TargetAmountInput, (inserted, targetStr) =>
        {
            return decimal.TryParse(targetStr, out var target) ? Math.Max(0, target - inserted) : 0m;
        }).ToBindableReactiveProperty(0m).AddTo(_disposables);

        ChangeAmount = InsertedAmount.CombineLatest(TargetAmountInput, (inserted, targetStr) =>
        {
            return decimal.TryParse(targetStr, out var target) ? Math.Max(0, inserted - target) : 0m;
        }).ToBindableReactiveProperty(0m).AddTo(_disposables);

        TransactionTimeoutSeconds = new BindableReactiveProperty<int>(60).AddTo(_disposables);

        // Map existing properties to the missing ones expected by XAML
        TotalTargetAmount = TargetAmount;
        CurrentAmount = InsertedAmount.ToReadOnlyReactiveProperty(0m).AddTo(_disposables);

        Progress = TargetAmount.CombineLatest(CurrentAmount, (target, current) =>
        {
            return target <= 0 ? 0.0 : Math.Min(100.0, (double)(current / target) * 100.0);
        }).ToReadOnlyReactiveProperty(0.0).AddTo(_disposables);

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
        AvailableDenominations = new ObservableCollection<DenominationViewModel>(
            getDenominations());

        InsertCashCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
        InsertCashCommand.Subscribe(den =>
        {
            if (_status.Value == PosTransactionStatus.WaitingForCash)
            {
                depositController.TrackDeposit(den.Key);
                LogOpos($"Cash inserted: {den.Name}");
            }
        });

        // Process Transaction Logic
        Observable.CombineLatest(InsertedAmount, _status, (inserted, status) => (inserted, status))
            .Subscribe(async x =>
            {
                var (inserted, status) = x;
                if (status != PosTransactionStatus.WaitingForCash) return;

                // Reset timeout on cash insertion
                ResetTimeout();

                if (decimal.TryParse(TargetAmountInput.Value, out var target))
                {
                    if (inserted >= target)
                    {
                        await CompleteTransactionAsync();
                    }
                }
            }).AddTo(_disposables);
    }

    private void StartTransaction()
    {
        _logger.LogInformation("Starting POS transaction for amount: {0}", TargetAmountInput.Value);
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
            _logger.LogError(pcEx, "Failed to start OPOS sequence: {0}", pcEx.Message);
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _status.Value = PosTransactionStatus.Idle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OPOS sequence: {0}", ex.Message);
            LogOpos($"ERROR: {ex.Message}");
            _status.Value = PosTransactionStatus.Idle;
        }
        _logger.LogInformation("StartTransaction finished. Status: {0}", _status.Value);
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
                _logger.LogWarning("Transaction timed out after {0} seconds.", timeoutSec);
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
        _logger.LogInformation("Canceling POS transaction.");
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

        _logger.LogInformation("Amount met. Completing transaction. Inserted: {0}, Target: {1}, Change: {2}", inserted, targetValue, changeToDispense);
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
            _logger.LogError(pcEx, "Failed to complete OPOS sequence: {0}", pcEx.Message);
            LogOpos($"POS ERROR [{pcEx.ErrorCode}]: {pcEx.Message}");
            _hardwareStatusManager.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete OPOS sequence: {0}", ex.Message);
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
        }
        catch (Exception ex)
        {
            LogOpos($"ERROR: {ex.Message}");
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
        GC.SuppressFinalize(this);
    }
}
