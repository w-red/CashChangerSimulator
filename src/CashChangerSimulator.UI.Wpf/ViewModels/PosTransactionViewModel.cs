using CashChangerSimulator.Core;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// POS取引（支払い、お釣り払い出し）フローを管理する ViewModel。
/// </summary>
public class PosTransactionViewModel : IDisposable
{
    private readonly DepositViewModel _deposit;
    private readonly DispenseViewModel _dispense;
    private readonly ILogger<PosTransactionViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

    // Properties
    public BindableReactiveProperty<string> TargetAmountInput { get; }
    public BindableReactiveProperty<PosTransactionStatus> TransactionStatus { get; }
    public BindableReactiveProperty<decimal> InsertedAmount { get; }
    public BindableReactiveProperty<decimal> RemainingAmount { get; }
    public BindableReactiveProperty<decimal> ChangeAmount { get; }

    // Commands
    public ReactiveCommand<Unit> StartCommand { get; }
    public ReactiveCommand<Unit> CancelCommand { get; }

    private readonly ReactiveProperty<PosTransactionStatus> _status = new(PosTransactionStatus.Idle);

    public PosTransactionViewModel(DepositViewModel deposit, DispenseViewModel dispense)
    {
        _deposit = deposit;
        _dispense = dispense;
        _logger = LogProvider.CreateLogger<PosTransactionViewModel>();

        TargetAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text) ? null :
                !decimal.TryParse(text, out var val) ? new Exception("Invalid amount") :
                val <= 0 ? new Exception("Amount must be positive") : null)
            .AddTo(_disposables);

        TransactionStatus = _status.ToBindableReactiveProperty().AddTo(_disposables);

        InsertedAmount = _deposit.CurrentDepositAmount.ToBindableReactiveProperty().AddTo(_disposables);

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

        // Process Transaction Logic
        InsertedAmount.Subscribe(async inserted =>
        {
            if (_status.Value != PosTransactionStatus.WaitingForCash) return;

            if (decimal.TryParse(TargetAmountInput.Value, out var target) && inserted >= target)
            {
                await CompleteTransactionAsync();
            }
        }).AddTo(_disposables);
    }

    private void StartTransaction()
    {
        _logger.ZLogInformation($"Starting POS transaction for amount: {TargetAmountInput.Value}");
        _status.Value = PosTransactionStatus.WaitingForCash;
        _deposit.BeginDepositCommand.Execute(Unit.Default);
    }

    private void CancelTransaction()
    {
        _logger.ZLogInformation($"Canceling POS transaction.");
        _deposit.CancelDepositCommand.Execute(Unit.Default);
        _status.Value = PosTransactionStatus.Idle;
    }

    private async Task CompleteTransactionAsync()
    {
        _logger.ZLogInformation($"Amount met. Completing transaction. Inserted: {InsertedAmount.Value}, Target: {TargetAmountInput.Value}");
        _status.Value = PosTransactionStatus.DispensingChange;

        // Fix and Store the deposit
        _deposit.FixDepositCommand.Execute(Unit.Default);
        await Task.Delay(200);
        _deposit.StoreDepositCommand.Execute(Unit.Default);
        await Task.Delay(200);

        var change = ChangeAmount.Value;
        if (change > 0)
        {
            _logger.ZLogInformation($"Dispensing change: {change}");
            _dispense.DispenseAmountInput.Value = change.ToString();
            _dispense.DispenseCommand.Execute(Unit.Default);

            // Wait for dispense to complete (roughly)
            // In a real app we'd observe Dispense.Status
            await Task.Delay(2000);
        }

        _logger.ZLogInformation($"Transaction completed successfully.");
        _status.Value = PosTransactionStatus.Completed;
        await Task.Delay(3000); // Show "Completed" for a while

        _status.Value = PosTransactionStatus.Idle;
        TargetAmountInput.Value = "";
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
