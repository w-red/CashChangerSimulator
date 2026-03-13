using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device.Services;

namespace CashChangerSimulator.UI.Cli.Services;

/// <summary>シミュレーションスクリプトの実行を管理するサービス。</summary>
/// <param name="scriptExecutionService">スクリプトの解析と低レベル実行を担当するサービス。</param>
/// <param name="console">実行状況を出力するコンソール。</param>
/// <param name="localizer">メッセージをローカライズするローカライザー。</param>
public class CliScriptService(
    IScriptExecutionService scriptExecutionService,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly IScriptExecutionService _scriptExecutionService = scriptExecutionService;

    public virtual async Task RunScriptAsync(string path)
    {
        if (!File.Exists(path))
        {
            _console.MarkupLine(_L["messages.file_not_found", path]);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            _console.MarkupLine(_L["messages.script_executing", path]);
            await _scriptExecutionService.ExecuteScriptAsync(json, op =>
            {
                _console.MarkupLine($"  [blue]>[/] {_L["messages.executing_op", op]}");
            });
            _console.MarkupLine(_L["messages.script_completed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}
