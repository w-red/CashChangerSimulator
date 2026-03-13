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
            _console.MarkupLine(_L["messages.device_opened"]);
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
            _console.MarkupLine(_L["messages.device_claimed"]);
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
            _console.MarkupLine(_L["messages.device_enabled"]);
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
            _console.MarkupLine(_L["messages.device_disabled"]);
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
            _console.MarkupLine(_L["messages.device_released"]);
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
            _console.MarkupLine(_L["messages.device_closed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    protected override string GetHint(ErrorCode errorCode)
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
