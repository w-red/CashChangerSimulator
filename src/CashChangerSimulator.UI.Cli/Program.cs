using System.Globalization;
using Cocona;
using Spectre.Console;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli.Services;

namespace CashChangerSimulator.UI.Cli;

/// <summary>CLI アプリケーションのメインエントリポイントを提供します。</summary>
public class Program
{
    /// <summary>アプリケーションを開始します。</summary>
    /// <param name="args">コマンドライン引数。</param>
    public static void Main(string[] args)
    {
        var (globalArgs, commandArgs) = ExtractGlobalOptions(args);

        var builder = CoconaApp.CreateBuilder(commandArgs);
        CliDIContainer.ConfigureServices(builder.Services, args);

        var app = builder.Build();
        app.AddCommands<CliCommands>();

        // Handle arguments globally
        var options = app.Services.GetRequiredService<CliSessionOptions>();
        ApplyGlobalOptions(globalArgs, options);

        if (commandArgs.Length == 0)
        {
            CliDIContainer.PostInitialize(app.Services, commandArgs);
            var shell = app.Services.GetRequiredService<CliInteractiveShell>();
            shell.RunAsync().GetAwaiter().GetResult();
        }
        else
        {
            // Single-shot mode
            CliDIContainer.PostInitialize(app.Services, commandArgs);
            app.Run();
        }
    }

    /// <summary>コマンドライン引数からグローバルオプションとコマンド引数を分離します。</summary>
    public static (string[] global, string[] command) ExtractGlobalOptions(string[] args)
    {
        var globals = new List<string>();
        var commands = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--async" || args[i] == "--verbose")
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

    /// <summary>抽出されたグローバルオプションをセッション設定に適用します。</summary>
    public static void ApplyGlobalOptions(string[] globalArgs, CliSessionOptions options)
    {
        for (int i = 0; i < globalArgs.Length; i++)
        {
            switch (globalArgs[i])
            {
                case "--async":
                    options.IsAsync = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
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

        // Always apply the selected language to the culture
        try
        {
            var culture = new CultureInfo(options.Language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
        catch
        {
            // Fallback if invalid language code
        }
    }
}
