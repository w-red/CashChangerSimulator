using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliDeviceService : CliServiceBase
{
    private readonly SimulatorCashChanger _changer;

    public CliDeviceService(
        SimulatorCashChanger changer,
        IAnsiConsole console,
        IStringLocalizer localizer) : base(console, localizer)
    {
        _changer = changer;
    }

    public void Open()
    {
        try
        {
            _changer.Open();
            _console.MarkupLine(_L["DeviceOpened"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Claim(int timeout)
    {
        try
        {
            _changer.Claim(timeout);
            _console.MarkupLine(_L["DeviceClaimed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Enable()
    {
        try
        {
            _changer.DeviceEnabled = true;
            _console.MarkupLine(_L["DeviceEnabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Disable()
    {
        try
        {
            _changer.DeviceEnabled = false;
            _console.MarkupLine(_L["DeviceDisabled"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Release()
    {
        try
        {
            _changer.Release();
            _console.MarkupLine(_L["DeviceReleased"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Close()
    {
        try
        {
            _changer.Close();
            _console.MarkupLine(_L["DeviceClosed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    protected override string GetHint(ErrorCode errorCode)
    {
        var hintKey = $"ErrorHint_{errorCode}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            return errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled ? (string)_L["ErrorHint_NotEnabled"] : (string)_L["ErrorHint_Generic"];
        }
        return hint;
    }
}
