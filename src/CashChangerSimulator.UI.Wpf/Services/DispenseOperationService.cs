using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary><see cref="IDispenseOperationService"/> の実装クラス（プレースホルダー）。</summary>
/// <summary>出金操作に関連するデバイス制御とエラーハンドリングを統括するサービス。</summary>
public class DispenseOperationService : IDispenseOperationService
{
    private readonly IDeviceFacade _facade;
    private readonly INotifyService _notifyService;
    private readonly ILogger<DispenseOperationService> _logger;
    private readonly ConfigurationProvider _configProvider;

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
        try
        {
            _facade.Dispense.DispenseChangeAsync(amount, true, (code, ext) => { }, _configProvider.Config.System.CurrencyCode)
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        _logger.ZLogError(t.Exception, $"Background dispense (amount) failed potentially late.");
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
        try
        {
            _facade.Dispense.DispenseCashAsync(counts, true, (code, ext) => { })
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        _logger.ZLogError(t.Exception, $"Background dispense (bulk) failed potentially late.");
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
