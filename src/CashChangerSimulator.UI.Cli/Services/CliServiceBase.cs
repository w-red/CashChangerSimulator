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

    public void ReportSuccess(string? message = null)
    {
        _console.MarkupLine($"[green][[{_L["messages.success_label"]}]][/] {(message ?? string.Empty)}");
    }

    public void HandleException(Exception ex)
    {
        if (ex is PosControlException pex)
        {
            var summary = GetSummary(pex.ErrorCode, pex.ErrorCodeExtended);
            var hint = GetHint(pex.ErrorCode, pex.ErrorCodeExtended);
            var errorLabel = _L["messages.error_label"];
            var summaryLabel = _L["messages.summary_label"];
            var codeLabel = _L["messages.code_label"];
            
            _console.MarkupLine($"[bold red][[{errorLabel}]][/] [red]{pex.Message}[/]");
            _console.MarkupLine($"  [yellow]{summaryLabel}:[/] {summary}");
            _console.MarkupLine($"  [yellow]{codeLabel}   :[/] {(int)pex.ErrorCode} ({pex.ErrorCode}) / {pex.ErrorCodeExtended}");
            
            if (!string.IsNullOrEmpty(hint))
            {
                _console.MarkupLine(_L["messages.hint_format", hint]);
            }
        }
        else
        {
            _console.MarkupLine($"[red][[{_L["messages.error_label"]}]][/] {ex.Message}");
        }
    }

    protected virtual string GetSummary(ErrorCode errorCode, int errorCodeExtended = 0)
    {
        var summaryKey = $"messages.error_summary_{errorCode.ToString().ToLowerInvariant()}";
        var summary = _L[summaryKey];
        return summary.ResourceNotFound ? (string)_L["messages.error_summary_generic"] : (string)summary;
    }

    protected virtual string GetHint(ErrorCode errorCode, int errorCodeExtended = 0)
    {
        var hintKey = $"messages.error_hint_{errorCode.ToString().ToLowerInvariant()}";
        var hint = _L[hintKey];
        return hint.ResourceNotFound ? (string)_L["messages.error_hint_generic"] : (string)hint;
    }
}
