using System;
using ConsoleAppFramework;
using Kokuban;

namespace CashChangerSimulator.UI.Cli;

public class Program
{
    public static void Main(string[] args)
    {
        CliDIContainer.Initialize(args);

        ConsoleApp.ServiceProvider = new CliResolverServiceProvider(CliDIContainer.Resolver);

        if (args.Length == 0)
        {
            RunInteractiveMode();
        }
        else
        {
            // Single-shot mode: use ConsoleAppFramework for full CLI parsing
            var app = ConsoleApp.Create();
            app.Add<CliCommands>();
            app.Run(args);
        }
    }

    private static void RunInteractiveMode()
    {
        var commands = CliDIContainer.Resolve<CliCommands>();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine();
            Console.WriteLine(Chalk.Yellow["Use 'exit' to quit."]);
        };

        Console.WriteLine(Chalk.Cyan.Bold["=== CashChangerSimulator CLI ==="]);
        Console.WriteLine("Type 'help' to see available commands.");
        Console.WriteLine("Type 'exit' to quit.");
        Console.WriteLine();

        while (true)
        {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null) break; // EOF
            if (string.IsNullOrWhiteSpace(line)) continue;

            var trimmed = line.Trim();
            if (trimmed == "exit" || trimmed == "quit") break;

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
                    case "readcashcounts":
                        commands.ReadCashCounts();
                        break;
                    case "deposit":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var amount))
                            commands.Deposit(amount);
                        else
                            Console.WriteLine(Chalk.Red["Usage: deposit <amount>"]);
                        break;
                    case "dispense":
                        if (parts.Length > 1 && int.TryParse(parts[1], out var dispAmt))
                            commands.Dispense(dispAmt);
                        else
                            Console.WriteLine(Chalk.Red["Usage: dispense <amount>"]);
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
                            Console.WriteLine(Chalk.Red["Usage: run-script <path>"]);
                        break;
                    case "help":
                        PrintHelp();
                        break;
                    default:
                        Console.WriteLine(Chalk.Red[$"Unknown command: {command}. Type 'help' for available commands."]);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(Chalk.Red[$"Error: {ex.Message}"]);
            }
            // Wait briefly for async log messages to flush before showing prompt
            Thread.Sleep(100);
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine(Chalk.Cyan.Bold["Available commands:"]);
        Console.WriteLine("  open                  - Open the device");
        Console.WriteLine("  claim [timeout]       - Claim exclusive access (default: 1000ms)");
        Console.WriteLine("  enable                - Enable the device");
        Console.WriteLine("  status                - Show device status and inventory");
        Console.WriteLine("  readCashCounts        - Read cash counts from device");
        Console.WriteLine("  deposit <amount>      - Begin deposit");
        Console.WriteLine("  dispense <amount>     - Dispense change");
        Console.WriteLine("  history [count]       - Show transaction history (default: 10)");
        Console.WriteLine("  disable               - Disable the device");
        Console.WriteLine("  release               - Release exclusive access");
        Console.WriteLine("  close                 - Close the device");
        Console.WriteLine("  run-script <path>     - Execute a JSON scenario script");
        Console.WriteLine("  help                  - Show this help");
        Console.WriteLine("  exit                  - Exit the CLI");
    }
}
