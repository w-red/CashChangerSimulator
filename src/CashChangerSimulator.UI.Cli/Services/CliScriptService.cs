using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliScriptService(
    IScriptExecutionService scriptExecutionService,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly IScriptExecutionService _scriptExecutionService = scriptExecutionService;

    public async Task RunScriptAsync(string path)
    {
        if (!File.Exists(path))
        {
            _console.MarkupLine(_L["FileNotFound", path]);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            _console.MarkupLine(_L["ScriptExecuting", path]);
            await _scriptExecutionService.ExecuteScriptAsync(json, op =>
            {
                _console.MarkupLine($"  [blue]>[/] Executing [cyan]{op}[/]");
            });
            _console.MarkupLine(_L["ScriptCompleted"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}
