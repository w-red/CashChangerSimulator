using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;
using System.IO;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>在庫操作およびデバイスの基本操作の実行ロジックを実装するサービスクラス。</summary>
public class InventoryOperationService : IInventoryOperationService
{
    private readonly ILogger<InventoryOperationService> _logger;
    private readonly IDeviceFacade _facade;
    private readonly ConfigurationProvider _configProvider;
    private readonly IHistoryExportService _exportService;
    private readonly INotifyService _notifyService;

    /// <summary>サービスを初期化します。</summary>
    public InventoryOperationService(
        IDeviceFacade facade,
        ConfigurationProvider configProvider,
        IHistoryExportService exportService,
        INotifyService notifyService,
        ILogger<InventoryOperationService> logger)
    {
        _facade = facade;
        _configProvider = configProvider;
        _exportService = exportService;
        _notifyService = notifyService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void OpenDevice()
    {
        try
        {
            _facade.Changer.Open();
            if (_facade.Changer.SkipStateVerification)
            {
                _facade.Changer.Claim(0);
                _facade.Changer.DeviceEnabled = true;
            }
        }
        catch (Exception ex)
        {
            _facade.Status.SetDeviceError((int)ErrorCode.Failure);
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }

    /// <inheritdoc/>
    public void CloseDevice()
    {
        try
        {
            _facade.Changer.Close();
        }
        catch (Exception ex)
        {
            _facade.Status.SetDeviceError((int)ErrorCode.Failure);
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }

    /// <inheritdoc/>
    public void ResetError()
    {
        _facade.Status.ResetError();
    }

    /// <inheritdoc/>
    public void CollectAll()
    {
        foreach (var monitor in _facade.Monitors.Monitors)
        {
            _facade.Inventory.SetCount(monitor.Key, 0);
        }
    }

    /// <inheritdoc/>
    public void ReplenishAll()
    {
        foreach (var monitor in _facade.Monitors.Monitors)
        {
            var setting = _configProvider.Config.GetDenominationSetting(monitor.Key);
            _facade.Inventory.SetCount(monitor.Key, setting.InitialCount);
        }
    }

    /// <inheritdoc/>
    public void ExportHistory()
    {
        var filter = ResourceHelper.GetAsString("Text.CsvFilter", "CSV files (*.csv)|*.csv");
        var fileName = $"History_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        
        var path = _facade.View.ShowSaveFileDialog(".csv", filter, fileName);
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var csv = _exportService.Export(_facade.History.Entries);
            File.WriteAllText(path, csv);
            _notifyService.ShowInfo(ResourceHelper.GetAsString("Text.ExportSuccess", "History exported successfully."), "Export");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to export history to {path}");
            _notifyService.ShowWarning(ResourceHelper.GetAsString("Text.ExportFailed", "Failed to export history.") + $": {ex.Message}", "Export");
        }
    }

    /// <inheritdoc/>
    public void SimulateJam()
    {
        _facade.Status.SetJammed(true);
    }

    /// <inheritdoc/>
    public void SimulateOverlap()
    {
        _facade.Status.SetOverlapped(true);
    }
}
