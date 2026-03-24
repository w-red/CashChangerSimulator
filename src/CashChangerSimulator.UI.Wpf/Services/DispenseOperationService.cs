using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>出金操作に関連するデバイス制御とエラーハンドリングを統括するサービス。</summary>
public class DispenseOperationService : IDispenseOperationService
{
    private readonly IDeviceFacade _facade;
    private readonly INotifyService _notifyService;
    private readonly ILogger<DispenseOperationService> _logger;
    private readonly ConfigurationProvider _configProvider;

    /// <summary>必要な依存コンポーネントを注入してサービスを初期化します。</summary>
    public DispenseOperationService(IDeviceFacade facade, INotifyService notifyService, ILogger<DispenseOperationService> logger, ConfigurationProvider configProvider)
    {
        _facade = facade;
        _notifyService = notifyService;
        _logger = logger;
        _configProvider = configProvider;
    }

    /// <summary>指定された金額の払い出しを開始します。</summary>
    public void DispenseCash(decimal amount)
    {
        if (_facade.Deposit.IsDepositInProgress)
        {
            var msg = ResourceHelper.GetAsString("WarnDispenseDuringDeposit", "Cannot dispense while deposit is in progress.");
            var title = ResourceHelper.GetAsString("Warn", "Warning");
            _notifyService.ShowWarning(msg, title);
            return;
        }

        try
        {
            _facade.Dispense.DispenseChangeAsync(amount, true, (code, ext) => { }, _configProvider.Config.System.CurrencyCode)
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        var ex = t.Exception?.Flatten().InnerException;
                        if (ex is PosControlException pcEx)
                        {
                            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
                        }
                        _logger.ZLogError(ex, $"Background dispense (amount) failed.");
                    }
                });
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to dispense {amount}.");
            _notifyService.ShowError(pcEx.Message);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense {amount}.");
            _notifyService.ShowError(ex.Message);
        }
    }

    /// <summary>指定された金種内訳での一括払い出しを実行します。</summary>
    public void ExecuteBulkDispense(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        if (_facade.Deposit.IsDepositInProgress)
        {
            _notifyService.ShowWarning(
                ResourceHelper.GetAsString("WarnDispenseDuringDeposit", "Cannot dispense while deposit is in progress."),
                ResourceHelper.GetAsString("Warn", "Warning"));
            return;
        }

        try
        {
            _facade.Dispense.DispenseCashAsync(counts, true, (code, ext) => { })
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        var ex = t.Exception?.Flatten().InnerException;
                        if (ex is PosControlException pcEx)
                        {
                            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
                        }
                        _logger.ZLogError(ex, $"Background dispense (bulk) failed.");
                    }
                });
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to dispense cash (bulk).");
            _notifyService.ShowError(pcEx.Message, ResourceHelper.GetAsString("DispenseError", "Dispense Error"));
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense cash (bulk).");
            _notifyService.ShowError(ex.Message, ResourceHelper.GetAsString("DispenseError", "Dispense Error"));
        }
    }
}
