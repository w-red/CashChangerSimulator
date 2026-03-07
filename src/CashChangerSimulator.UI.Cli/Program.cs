using System.Globalization;
using Cocona;
using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Cli;

public class Program
{
    public static void Main(string[] args)
    {
        var (globalArgs, commandArgs) = ExtractGlobalOptions(args);

        var builder = CoconaApp.CreateBuilder(commandArgs);
        CliDIContainer.ConfigureServices(builder.Services, commandArgs);

        var app = builder.Build();
        app.AddCommands<CliCommands>();

        // Handle arguments globally
        var options = app.Services.GetRequiredService<CliSessionOptions>();
        ApplyGlobalOptions(globalArgs, options);

        if (commandArgs.Length == 0)
        {
            CliDIContainer.PostInitialize(app.Services, commandArgs);
            RunInteractiveMode(app.Services);
        }
        else
        {
            // Single-shot mode
            CliDIContainer.PostInitialize(app.Services, commandArgs);
            app.Run();
        }
    }

    private static (string[] global, string[] command) ExtractGlobalOptions(string[] args)
    {
        var globals = new List<string>();
        var commands = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--async")
            {
                globals.Add(args[i]);
            }
            else if ((args[i] == "--lang" || args[i] == "--currency") && i + 1 < args.Length)
            {
                globals.Add(args[i]);
                globals.Add(args[++i]);
            }
            else
            {
                commands.Add(args[i]);
            }
        }
        return (globals.ToArray(), commands.ToArray());
    }

    private static void ApplyGlobalOptions(string[] globalArgs, CliSessionOptions options)
    {
        for (int i = 0; i < globalArgs.Length; i++)
        {
            switch (globalArgs[i])
            {
                case "--async":
                    options.IsAsync = true;
                    break;
                case "--lang":
                    if (i + 1 < globalArgs.Length)
                    {
                        var lang = globalArgs[++i];
                        options.Language = lang;
                        try
                        {
                            var culture = new CultureInfo(lang);
                            CultureInfo.DefaultThreadCurrentCulture = culture;
                            CultureInfo.DefaultThreadCurrentUICulture = culture;
                            Thread.CurrentThread.CurrentCulture = culture;
                            Thread.CurrentThread.CurrentUICulture = culture;
                            Thread.CurrentThread.CurrentUICulture = culture;
                        }
                        catch
                        {
                        }
                    }
                    break;
                case "--currency":
                    if (i + 1 < globalArgs.Length) options.CurrencyCode = globalArgs[++i].ToUpperInvariant();
                    break;
            }
        }
    }

    private static void RunInteractiveMode(IServiceProvider services)
    {
        var commands = services.GetRequiredService<CliCommands>();
        var changer = services.GetRequiredService<SimulatorCashChanger>();
        var console = services.GetRequiredService<IAnsiConsole>();
        var options = services.GetRequiredService<CliSessionOptions>();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Use 'exit' to quit.[/]");
        };

        AnsiConsole.Write(new FigletText("Cash Changer").Color(Color.Cyan));
        AnsiConsole.Write(new Rule("[cyan]Simulator CLI[/]").LeftJustified());
        AnsiConsole.MarkupLine("Type [bold yellow]help[/] to see available commands.");
        AnsiConsole.MarkupLine("Type [bold yellow]exit[/] to quit.");
        AnsiConsole.WriteLine();

        // Setup ReadLine
        var commandList = new[] { "open", "claim", "enable", "disable", "status", "read-counts", "deposit", "fix-deposit", "end-deposit", "dispense", "adjust-counts", "history", "release", "close", "run-script", "config", "log-level", "help", "exit", "quit" };
        ReadLine.AutoCompletionHandler = new CliAutoCompleteHandler(commandList);
        ReadLine.HistoryEnabled = true;

        var historyFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CashChangerSimulator", "cli_history.txt");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(historyFile)!);
            if (File.Exists(historyFile))
            {
                ReadLine.AddHistory(File.ReadAllLines(historyFile));
            }
        }
        catch { }

        while (true)
        {
            var prompt = options.IsAsync ? "async > " : "> ";
            var line = ReadLine.Read(prompt);

            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.Trim();
            var lower = trimmed.ToLowerInvariant();
            if (lower is "exit" or "quit")
            {
                if (ConfirmExit(changer, console)) break;
                continue;
            }

            // Persistence
            try { File.AppendAllLines(historyFile, new[] { trimmed }); } catch { }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = parts[0].ToLowerInvariant();

            try
            {
                switch (command)
                {
                    case "open":
                        commands.Open();
                        break;
                    case "claim":
                        var timeout = parts.Length > 1 && int.TryParse(parts[1], out var t) ? t : 1000;
                        commands.Claim(timeout);
                        break;
                    case "enable":
                        commands.Enable();
                        break;
                    case "disable":
                        commands.Disable();
                        break;
                    case "status":
                        commands.Status();
                        break;
                    case "read-counts":
                        commands.ReadCashCounts();
                        break;
                    case "deposit":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var amount))
                            commands.Deposit(amount);
                        else
                            commands.Deposit(null);
                        break;
                    case "fix-deposit":
                        commands.FixDeposit();
                        break;
                    case "end-deposit":
                        commands.EndDeposit();
                        break;
                    case "adjust-counts":
                        if (parts.Length > 1)
                            commands.AdjustCashCounts(parts[1]);
                        else
                            AnsiConsole.MarkupLine("[red]Usage: adjust-counts <value:count,value:count>[/]");
                        break;
                    case "dispense":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var dispAmt))
                            commands.Dispense(dispAmt);
                        else
                            AnsiConsole.MarkupLine("[red]Usage: dispense <amount>[/]");
                        break;
                    case "history":
                        var count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 10;
                        commands.History(count);
                        break;
                    case "release":
                        commands.Release();
                        break;
                    case "close":
                        commands.Close();
                        break;
                    case "run-script":
                        if (parts.Length > 1)
                            commands.RunScript(parts[1]).GetAwaiter().GetResult();
                        else
                            AnsiConsole.MarkupLine("[red]Usage: run-script <path>[/]");
                        break;
                    case "config":
                        if (parts.Length > 1)
                        {
                            var sub = parts[1].ToLowerInvariant();
                            switch (sub)
                            {
                                case "list": commands.ConfigList(); break;
                                case "get": if (parts.Length > 2) commands.ConfigGet(parts[2]); break;
                                case "set": if (parts.Length > 3) commands.ConfigSet(parts[2], parts[3]); break;
                                case "save": commands.ConfigSave(); break;
                                case "reload": commands.ConfigReload(); break;
                                default: commands.Config(); break;
                            }
                        }
                        else
                        {
                            commands.Config();
                        }
                        break;
                    case "log-level":
                        if (parts.Length > 1)
                            commands.LogLevel(parts[1]);
                        else
                            AnsiConsole.MarkupLine("[red]Usage: log-level <level>[/]");
                        break;
                    case "help":
                        commands.Help();
                        break;
                    default:
                        AnsiConsole.MarkupLine($"[red]Unknown command: {command}. Type 'help' for available commands.[/]");
                        break;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }
            Thread.Sleep(100);
        }

        try { changer.Close(); } catch { }
    }

    private static bool ConfirmExit(SimulatorCashChanger changer, IAnsiConsole console)
    {
        var isOpen = changer.State != Microsoft.PointOfService.ControlState.Closed;
        if (isOpen)
        {
            console.MarkupLine("[yellow]Warning: Device is still open or processing.[/]");
            if (!console.Confirm("Are you sure you want to exit? (Device will be closed automatically)"))
            {
                return false;
            }
        }
        return true;
    }
}

public class CliAutoCompleteHandler(string[] commands) : IAutoCompleteHandler
{
    private readonly string[] _commands = commands;
    public char[] Separators { get; set; } = [' '];
    public string[] GetSuggestions(string text, int index)
    {
        return string.IsNullOrWhiteSpace(text) ? _commands : [.. _commands.Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase))];
    }
}
