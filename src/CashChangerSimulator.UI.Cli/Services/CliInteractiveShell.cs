using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Cli.Services;

/// <summary>CLI の対話型ループ (REPL) を制御するクラス。</summary>
public class CliInteractiveShell(
    ICliCommandDispatcher dispatcher,
    SimulatorCashChanger changer,
    IAnsiConsole console,
    IStringLocalizer localizer,
    CliSessionOptions options,
    ILineReader reader)
{
    private readonly ICliCommandDispatcher _dispatcher = dispatcher;
    private readonly SimulatorCashChanger _changer = changer;
    private readonly IAnsiConsole _console = console;
    private readonly IStringLocalizer _L = localizer;
    private readonly CliSessionOptions _options = options;
    private readonly ILineReader _reader = reader;

    /// <summary>インタラクティブモードを開始します。</summary>
    public async Task RunAsync()
    {
        SetupCancelHandler();
        ShowWelcomeMessage();
        SetupReadLine();
        SetupHistory();

        while (true)
        {
            var prompt = _options.IsAsync ? "async > " : "> ";
            var line = _reader.Read(prompt);

            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.Trim();
            var lower = trimmed.ToLowerInvariant();
            if (lower is "exit" or "quit")
            {
                if (ConfirmExit()) break;
                continue;
            }

            SaveHistory(trimmed);

            try
            {
                await _dispatcher.DispatchAsync(trimmed);
            }
            catch (Exception ex)
            {
                _console.MarkupLine(_L["messages.error_prefix", ex.Message]);
            }
            
            // Give some time for async operations or UI updates if needed
            await Task.Delay(100);
        }

        try { _changer.Close(); } catch { }
    }

    private void SetupCancelHandler()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _console.WriteLine();
            _console.MarkupLine(_L["messages.exit_hint"]);
        };
    }

    private void ShowWelcomeMessage()
    {
        _console.Write(new FigletText(_L["messages.welcome"]).Color(Color.Cyan));
        _console.Write(new Rule($"[cyan]{_L["messages.simulator_cli"]}[/]").LeftJustified());
        _console.MarkupLine(_L["messages.help_hint"]);
        _console.MarkupLine(_L["messages.exit_hint"]);
        _console.WriteLine();
    }

    private void SetupReadLine()
    {
        var commandList = new[] { 
            "open", "claim", "enable", "disable", "status", "read-counts", 
            "deposit", "fix-deposit", "end-deposit", "dispense", "adjust-counts", 
            "history", "release", "close", "run-script", 
            "config", "config list", "config get", "config set", "config save", "config reload",
            "log-level", "log-level Debug", "log-level Information", "log-level Warning", "log-level Error",
            "help", "exit", "quit" 
        };
        ReadLine.AutoCompletionHandler = new CliAutoCompleteHandler(commandList);
        ReadLine.HistoryEnabled = true;
    }

    private string GetHistoryFile() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "CashChangerSimulator", 
        "cli_history.txt");

    private void SetupHistory()
    {
        var historyFile = GetHistoryFile();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyFile)!);
            if (File.Exists(historyFile))
            {
                var historyLines = File.ReadAllLines(historyFile).Distinct().ToArray();
                _reader.AddHistory(historyLines);
            }
        }
        catch { }
    }

    private void SaveHistory(string line)
    {
        var historyFile = GetHistoryFile();
        try
        {
            var history = File.Exists(historyFile) ? File.ReadAllLines(historyFile).ToList() : [];
            if (history.Count == 0 || history[^1] != line)
            {
                File.AppendAllLines(historyFile, [line]);
            }
        }
        catch { }
    }

    private bool ConfirmExit()
    {
        var isOpen = _changer.State != Microsoft.PointOfService.ControlState.Closed;
        if (isOpen)
        {
            _console.MarkupLine(_L["messages.exit_warning"]);
            if (!_console.Confirm(_L["messages.exit_confirm"]))
            {
                return false;
            }
        }
        return true;
    }
}
