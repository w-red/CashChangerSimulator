using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.UI.Cli.Services;

/// <summary>
/// CLI の各サービスクラスの基底クラス。
/// </summary>
public abstract class CliServiceBase(IAnsiConsole console, IStringLocalizer localizer)
{
    protected readonly IAnsiConsole _console = console;
    protected readonly IStringLocalizer _L = localizer;

    public void HandleException(Exception ex)
    {
        if (ex is PosControlException pex)
        {
            var hint = GetHint(pex.ErrorCode);
            var errMsg = _L["ErrorFormat", "Error", (int)pex.ErrorCode, pex.ErrorCodeExtended, pex.Message];
            _console.MarkupLine(errMsg);
            if (!string.IsNullOrEmpty(hint))
            {
                _console.MarkupLine(_L["HintFormat", hint]);
            }
        }
        else
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    protected virtual string GetHint(ErrorCode errorCode)
    {
        var hintKey = $"ErrorHint_{errorCode}";
        var hint = _L[hintKey];
        return hint.ResourceNotFound ? (string)_L["ErrorHint_Generic"] : (string)hint;
    }
}
