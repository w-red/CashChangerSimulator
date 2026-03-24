using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliDeviceService(
    SimulatorCashChanger changer,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly SimulatorCashChanger _changer = changer;

    public virtual void Open()
    {
        try
        {
            _changer.Open();
            ReportSuccess(_L["messages.device_opened"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Claim(int timeout)
    {
        try
        {
            _changer.Claim(timeout);
            ReportSuccess(_L["messages.device_claimed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Enable()
    {
        try
        {
            _changer.DeviceEnabled = true;
            ReportSuccess(_L["messages.device_enabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Disable()
    {
        try
        {
            _changer.DeviceEnabled = false;
            ReportSuccess(_L["messages.device_disabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Release()
    {
        try
        {
            _changer.Release();
            ReportSuccess(_L["messages.device_released"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Close()
    {
        try
        {
            _changer.Close();
            ReportSuccess(_L["messages.device_closed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void SetCollectionBoxRemoved(bool removed)
    {
        try
        {
            _changer.HardwareStatus.SetCollectionBoxRemoved(removed);
            var msg = removed ? _L["messages.box_removed"] : _L["messages.box_inserted"];
            ReportSuccess(msg);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void ResetBox()
    {
        try
        {
            _changer.HardwareStatus.SetCollectionBoxRemoved(false);
            ReportSuccess(_L["messages.box_reset"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    protected override string GetSummary(ErrorCode errorCode, int errorCodeExtended = 0)
    {
        if (errorCode == ErrorCode.Extended)
        {
            // UPOS Standard: Full = 206, Empty = 205
            if (errorCodeExtended == 206) return (string)_L["messages.error_summary_full"];
            if (errorCodeExtended == 205) return (string)_L["messages.error_summary_empty"];
        }

        var summaryKey = $"messages.error_summary_{errorCode.ToString().ToLowerInvariant()}";
        var summary = _L[summaryKey];
        if (summary.ResourceNotFound)
        {
            return errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled ? (string)_L["messages.error_summary_illegal"] : (string)_L["messages.error_summary_generic"];
        }
        return summary;
    }

    protected override string GetHint(ErrorCode errorCode, int errorCodeExtended = 0)
    {
        var hintKey = $"messages.error_hint_{errorCode.ToString().ToLowerInvariant()}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            return errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled ? (string)_L["messages.error_hint_notenabled"] : (string)_L["messages.error_hint_generic"];
        }
        return hint;
    }
}
