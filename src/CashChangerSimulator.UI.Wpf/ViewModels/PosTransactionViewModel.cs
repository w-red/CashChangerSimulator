using CashChangerSimulator.Core;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;
using System.Collections.ObjectModel;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>POS取引（支払い、お釣り払い出し）フローを管理する ViewModel。</summary>
public class PosTransactionViewModel : IDisposable
{
    private readonly DepositViewModel _deposit;
    private readonly DispenseViewModel _dispense;
    private readonly SimulatorCashChanger _cashChanger;
    private readonly ILogger<PosTransactionViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

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
    /// <summary>OPOSアクションのログ。</summary>
    public ObservableCollection<string> OposLog { get; } = new();

    // Commands
    /// <summary>取引を開始するコマンド。</summary>
    public ReactiveCommand<Unit> StartCommand { get; }
    /// <summary>取引をキャンセルするコマンド。</summary>
    public ReactiveCommand<Unit> CancelCommand { get; }

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
    public PosTransactionViewModel(DepositViewModel deposit, DispenseViewModel dispense, SimulatorCashChanger cashChanger)
    {
        _deposit = deposit;
        _dispense = dispense;
        _cashChanger = cashChanger;
        _logger = LogProvider.CreateLogger<PosTransactionViewModel>();

        TargetAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text) ? null :
                !decimal.TryParse(text, out var val) ? new Exception("Invalid amount") :
                val <= 0 ? new Exception("Amount must be positive") : null)
            .AddTo(_disposables);

        TargetAmount = TargetAmountInput.Select(text =>
        {
            return decimal.TryParse(text, out var val) ? val : 0m;
        }).ToReadOnlyReactiveProperty(0m).AddTo(_disposables);

        TransactionStatus = _status.ToBindableReactiveProperty().AddTo(_disposables);

        InsertedAmount = _deposit.CurrentDepositAmount;

        RemainingAmount = InsertedAmount.CombineLatest(TargetAmountInput, (inserted, targetStr) =>
        {
            if (decimal.TryParse(targetStr, out var target))
            {
                return Math.Max(0, target - inserted);
            }
            return 0m;
        }).ToBindableReactiveProperty(0m).AddTo(_disposables);

        ChangeAmount = InsertedAmount.CombineLatest(TargetAmountInput, (inserted, targetStr) =>
        {
            if (decimal.TryParse(targetStr, out var target))
            {
                return Math.Max(0, inserted - target);
            }
            return 0m;
        }).ToBindableReactiveProperty(0m).AddTo(_disposables);

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

        // Process Transaction Logic
        Observable.CombineLatest(InsertedAmount, _status, (inserted, status) => (inserted, status))
            .Subscribe(async x =>
            {
                var (inserted, status) = x;
                if (status != PosTransactionStatus.WaitingForCash) return;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start OPOS sequence: {0}", ex.Message);
            LogOpos($"ERROR: {ex.Message}");
            _status.Value = PosTransactionStatus.Idle;
        }
        _logger.LogInformation("StartTransaction finished. Status: {0}", _status.Value);
    }

    private void LogOpos(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        OposLog.Add($"[{timestamp}] {message}");
    }

    private void CancelTransaction()
    {
        _logger.LogInformation("Canceling POS transaction.");
        LogOpos("Cancelling... EndDeposit(Repay)");
        
        try
        {
            _cashChanger.EndDeposit(CashDepositAction.Repay);
            LogOpos("Release()");
            _cashChanger.Release();
            LogOpos("Close()");
            _cashChanger.Close();
        }
        catch (Exception ex)
        {
            LogOpos($"Error during cancel: {ex.Message}");
        }

        _status.Value = PosTransactionStatus.Idle;
    }

    private async Task CompleteTransactionAsync()
    {
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
            _cashChanger.EndDeposit(Microsoft.PointOfService.CashDepositAction.NoChange);
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
