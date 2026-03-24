using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>入金操作に関連するデバイス制御とエラーハンドリングを統括するサービス。</summary>
public class DepositOperationService : IDepositOperationService
{
    private readonly IDeviceFacade _facade;
    private readonly INotifyService _notifyService;
    private readonly ILogger<DepositOperationService> _logger;

    /// <summary>必要な依存コンポーネントを注入してサービスを初期化します。</summary>
    public DepositOperationService(IDeviceFacade facade, INotifyService notifyService, ILogger<DepositOperationService> logger)
    {
        _facade = facade;
        _notifyService = notifyService;
        _logger = logger;
    }

    /// <summary>入金処理を開始します。</summary>
    public void BeginDeposit()
    {
        if (_facade.Dispense.Status == CashDispenseStatus.Busy)
        {
            var msg = ResourceHelper.GetAsString("WarnDepositDuringDispense", "Cannot begin deposit while dispense is in progress.");
            var title = ResourceHelper.GetAsString("Warn", "Warning");
            _notifyService.ShowWarning(msg, title);
            return;
        }

        try
        {
            _facade.Deposit.BeginDeposit();
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to begin deposit.");
            _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to begin deposit.");
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }

    /// <summary>入金処理を一時停止または再開します。</summary>
    public void PauseDeposit(CashDepositPause control)
    {
        try
        {
            _facade.Deposit.PauseDeposit(control);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to pause/resume deposit.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>入金金額を確定します。</summary>
    public void FixDeposit()
    {
        try
        {
            _facade.Deposit.FixDeposit();
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to fix deposit.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>入金処理を終了します。</summary>
    public void EndDeposit(CashDepositAction action)
    {
        try
        {
            _facade.Deposit.EndDeposit(action);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to end deposit.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>一括入金状況を追跡します。</summary>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        try
        {
            _facade.Deposit.TrackBulkDeposit(counts);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to track bulk deposit.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>リジェクト動作をシミュレートします。</summary>
    public void SimulateReject(decimal amount)
    {
        try
        {
            _facade.Deposit.SimulateReject(amount);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to simulate reject.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>目標金額に達するまで自動的に入金を行うクイック入金を実行します。</summary>
    public async Task ExecuteQuickDepositAsync(decimal targetAmount, IEnumerable<DenominationKey> denominations)
    {
        if (_facade.Dispense.Status == CashDispenseStatus.Busy)
        {
            _notifyService.ShowWarning(
                ResourceHelper.GetAsString("WarnDepositDuringDispense", "Cannot begin deposit while dispense is in progress."),
                ResourceHelper.GetAsString("Warn", "Warning"));
            return;
        }

        try
        {
            _facade.Deposit.BeginDeposit();
            var breakdown = new Dictionary<DenominationKey, int>();
            var remaining = targetAmount;
            var sortedDens = denominations.OrderByDescending(d => d.Value);

            foreach (var den in sortedDens)
            {
                if (den.Value <= 0) continue;
                int count = (int)(remaining / den.Value);
                if (count > 0)
                {
                    breakdown[den] = count;
                    remaining -= count * den.Value;
                }
            }

            if (breakdown.Count > 0)
            {
                _facade.Deposit.TrackBulkDeposit(breakdown);
            }

            await Task.Delay(100);
            _facade.Deposit.FixDeposit();
            await Task.Delay(100);
            _facade.Deposit.EndDeposit(CashDepositAction.NoChange);
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to execute quick deposit.");
            _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to execute quick deposit.");
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }
}
