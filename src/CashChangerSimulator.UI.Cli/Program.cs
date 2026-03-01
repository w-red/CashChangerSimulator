using ConsoleAppFramework;

namespace CashChangerSimulator.UI.Cli;

public class Program
{
    public static void Main(string[] args)
    {
        CliDIContainer.Initialize();

        var app = ConsoleApp.Create();
        app.Add<CliCommands>();
        
        if (args.Length == 0)
        {
            Console.WriteLine("=== CashChangerSimulator CLI ===");
            Console.WriteLine("Type 'help' to see available commands.");
            Console.WriteLine("Type 'exit' to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line == "exit" || line == "quit") break;

                var subArgs = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                app.Run(subArgs);
            }
        }
        else
        {
            app.Run(args);
        }
    }
}
