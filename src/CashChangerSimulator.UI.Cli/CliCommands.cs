using Cocona;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core;

namespace CashChangerSimulator.UI.Cli;

/// <summary>CLI からシミュレータを操作するためのコマンドを提供します。</summary>
public partial class CliCommands
{
    private readonly CliDeviceService _deviceService;
    private readonly CliCashService _cashService;
    private readonly CliConfigService _configService;
    private readonly CliViewService _viewService;
    private readonly CliScriptService _scriptService;
    private readonly SimulatorCashChanger _changer;
    private readonly IAnsiConsole _console;
    private readonly IStringLocalizer _L;

    /// <summary>CliCommands の新しいインスタンスを初期化します。</summary>
    public CliCommands(
        SimulatorCashChanger changer,
        CliDeviceService deviceService,
        CliCashService cashService,
        CliConfigService configService,
        CliViewService viewService,
        CliScriptService scriptService,
        IAnsiConsole console,
        IStringLocalizer localizer)
    {
        _changer = changer;
        _deviceService = deviceService;
        _cashService = cashService;
        _configService = configService;
        _viewService = viewService;
        _scriptService = scriptService;
        _console = console;
        _L = localizer;
    }

    /// <summary>非同期エラーイベントをハンドリングし、エラーメッセージを表示します。</summary>
    public void HandleAsyncError(object sender, DeviceErrorEventArgs e)
    {
        _console.WriteLine();
        var hint = GetHint(e.ErrorCode);
        var errMsg = _L["ErrorFormat", "Async Error", (int)e.ErrorCode, e.ErrorCodeExtended, "Async operation failed"];
        _console.MarkupLine(errMsg);
        if (!string.IsNullOrEmpty(hint))
        {
            _console.MarkupLine(_L["HintFormat", hint]);
        }
    }

    private string GetHint(ErrorCode errorCode)
    {
        var hintKey = $"ErrorHint_{errorCode}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            return errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled ? (string)_L["ErrorHint_NotEnabled"] : (string)_L["ErrorHint_Generic"];
        }
        return hint;
    }

    /// <summary>指定された JSON スクリプトファイルを実行します。</summary>
    [Command("run-script")]
    public Task RunScript(string path) => _scriptService.RunScriptAsync(path);

    /// <summary>デバイスの状態と現在の在高を表示します。</summary>
    [Command("status")]
    public void Status() => _viewService.Status();

    /// <summary>取引履歴を表示します。</summary>
    [Command("history")]
    public void History([Option('c', Description = "表示件数")] int count = 10) => _viewService.History(count);

    /// <summary>デバイスをオープンします。</summary>
    [Command("open")]
    public void Open() => _deviceService.Open();

    /// <summary>デバイスを占有します。</summary>
    [Command("claim")]
    public void Claim([Option('t', Description = "タイムアウト(ms)")] int timeout = 5000) => _deviceService.Claim(timeout);

    /// <summary>デバイスを有効化します。</summary>
    [Command("enable")]
    public void Enable() => _deviceService.Enable();

    /// <summary>現在の在高を読み取ります。</summary>
    [Command("read-counts")]
    public void ReadCashCounts() => _cashService.ReadCashCounts();

    /// <summary>入金を開始します。</summary>
    [Command("deposit")]
    public void Deposit(int? amount = null) => _cashService.Deposit(amount);

    /// <summary>入金を確定します。</summary>
    [Command("fix-deposit")]
    public void FixDeposit() => _cashService.FixDeposit();

    /// <summary>入金を終了します。</summary>
    [Command("end-deposit")]
    public void EndDeposit() => _cashService.EndDeposit();

    /// <summary>出金を実行します。</summary>
    [Command("dispense")]
    public void Dispense(int amount) => _cashService.Dispense(amount);

    /// <summary>デバイスを無効化します。</summary>
    [Command("disable")]
    public void Disable() => _deviceService.Disable();

    /// <summary>デバイスの占有を解除します。</summary>
    [Command("release")]
    public void Release() => _deviceService.Release();

    /// <summary>デバイスをクローズします。</summary>
    [Command("close")]
    public void Close() => _deviceService.Close();

    /// <summary>設定を一覧表示または変更します。</summary>
    [Command("config")]
    public void Config() => _console.MarkupLine("[yellow]Usage: config <list|get|set|save>[/]");

    /// <summary>設定項目を一覧表示します。</summary>
    [Command("config list")]
    public void ConfigList() => _configService.List();

    /// <summary>特定の設定値を取得します。</summary>
    [Command("config get")]
    public void ConfigGet(string key) => _configService.Get(key);

    /// <summary>設定値を一時的に更新します。</summary>
    [Command("config set")]
    public void ConfigSet(string key, string value) => _configService.Set(key, value);

    /// <summary>更新した設定を TOML ファイルに保存します。</summary>
    [Command("config save")]
    public void ConfigSave() => _configService.Save();

    /// <summary>設定をファイルから再読み込みします。</summary>
    [Command("config reload")]
    public void ConfigReload() => _configService.Reload();

    /// <summary>ログの詳細度を変更します。</summary>
    [Command("log-level")]
    public void LogLevel(string level)
    {
        if (System.Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(level, true, out _))
        {
            LogProvider.SetLogLevel(level);
            _console.MarkupLine(_L["LogLevelUpdated", level]);
        }
        else
        {
            _console.MarkupLine(_L["InvalidLogLevel", level]);
        }
    }

    /// <summary>利用可能なコマンドの一覧を表示します。</summary>
    [Command("help")]
    public void Help()
    {
        _console.Write(new Rule($"[cyan]{_L["AvailableCommands"]}[/]").LeftJustified());
        var table = new Table().Border(TableBorder.None);
        table.AddColumn(_L["CommandLabel"]);
        table.AddColumn(_L["DescriptionLabel"]);

        table.AddRow(Markup.Escape("open"), "Open device");
        table.AddRow(Markup.Escape("claim"), "Claim device");
        table.AddRow(Markup.Escape("enable"), "Enable device");
        table.AddRow(Markup.Escape("status"), "Show status & inventory");
        table.AddRow(Markup.Escape("read-counts"), "Read cash counts");
        table.AddRow(Markup.Escape("deposit [amount]"), "Start deposit");
        table.AddRow(Markup.Escape("fix-deposit"), "Fix current deposit");
        table.AddRow(Markup.Escape("end-deposit"), "End deposit session");
        table.AddRow(Markup.Escape("dispense <amount>"), "Dispense change");
        table.AddRow(Markup.Escape("disable"), "Disable device");
        table.AddRow(Markup.Escape("release"), "Release device");
        table.AddRow(Markup.Escape("close"), "Close device");
        table.AddRow(Markup.Escape("history"), "Show transaction history");
        table.AddRow(Markup.Escape("config <list|get|set|save|reload>"), "Manage configuration");
        table.AddRow(Markup.Escape("log-level <level>"), _L["LogLevelDescription"] ?? "Change log level");
        table.AddRow(Markup.Escape("run-script <path>"), "Run JSON script");
        table.AddRow(Markup.Escape("help"), Markup.Escape(_L["HelpDescription"] ?? ""));
        table.AddRow(Markup.Escape("exit"), Markup.Escape(_L["ExitDescription"] ?? ""));

        _console.Write(table);
    }
}
