using Cocona;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Core;

namespace CashChangerSimulator.UI.Cli;

/// <summary>CLI からシミュレータを操作するためのコマンドを提供します。</summary>
/// <param name="changer">シミュレータ本体のインスタンス。</param>
/// <param name="deviceService">デバイスの基本操作（Open, Claim等）を提供するサービス。</param>
/// <param name="cashService">現金操作（入出金）を提供するサービス。</param>
/// <param name="configService">設定情報の管理を提供するサービス。</param>
/// <param name="viewService">状態や履歴の表示を提供するサービス。</param>
/// <param name="scriptService">自動実行スクリプトの実行機能を提供するサービス。</param>
/// <param name="console">CLI への出力を行うためのコンソールインターフェース。</param>
/// <param name="localizer">多言語対応メッセージを提供するローカライザー。</param>
public partial class CliCommands(
    SimulatorCashChanger changer,
    CliDeviceService deviceService,
    CliCashService cashService,
    CliConfigService configService,
    CliViewService viewService,
    CliScriptService scriptService,
    IAnsiConsole console,
    IStringLocalizer localizer)
{
    private readonly CliDeviceService _deviceService = deviceService;
    private readonly CliCashService _cashService = cashService;
    private readonly CliConfigService _configService = configService;
    private readonly CliViewService _viewService = viewService;
    private readonly CliScriptService _scriptService = scriptService;
    private readonly SimulatorCashChanger _changer = changer;
    private readonly IAnsiConsole _console = console;
    private readonly IStringLocalizer _L = localizer;

    /// <summary>非同期エラーイベントをハンドリングし、エラーメッセージを表示します。</summary>
    public void HandleAsyncError(object sender, DeviceErrorEventArgs e)
    {
        _console.WriteLine();
        var hint = GetHint(e.ErrorCode);
        var errMsg = _L["messages.error_format", "Async Error", (int)e.ErrorCode, e.ErrorCodeExtended, "Async operation failed"];
        _console.MarkupLine(errMsg);
        if (!string.IsNullOrEmpty(hint))
        {
            _console.MarkupLine(_L["messages.hint_format", hint]);
        }
    }

    private string GetHint(ErrorCode errorCode)
    {
        var hintKey = $"messages.error_hint_{errorCode.ToString().ToLowerInvariant()}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            return errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled ? (string)_L["messages.error_hint_notenabled"] : (string)_L["messages.error_hint_generic"];
        }
        return hint;
    }

    /// <summary>指定された JSON スクリプトファイルを実行します。</summary>
    [Command("run-script")]
    public virtual Task RunScript(string path) => _scriptService.RunScriptAsync(path);

    /// <summary>デバイスの状態と現在の在高を表示します。</summary>
    [Command("status")]
    public virtual void Status() => _viewService.Status();

    /// <summary>取引履歴を表示します。</summary>
    [Command("history")]
    public virtual void History([Option('c', Description = "表示件数")] int count = 10) => _viewService.History(count);

    /// <summary>デバイスをオープンします。</summary>
    [Command("open")]
    public virtual void Open() => _deviceService.Open();

    /// <summary>デバイスを占有します。</summary>
    [Command("claim")]
    public virtual void Claim([Option('t', Description = "タイムアウト(ms)")] int timeout = 5000) => _deviceService.Claim(timeout);

    /// <summary>デバイスを有効化します。</summary>
    [Command("enable")]
    public virtual void Enable() => _deviceService.Enable();

    /// <summary>現在の在高を読み取ります。</summary>
    [Command("read-counts")]
    public virtual void ReadCashCounts() => _cashService.ReadCashCounts();

    /// <summary>入金を開始します。</summary>
    [Command("deposit")]
    public virtual void Deposit(int? amount = null) => _cashService.Deposit(amount);

    /// <summary>入金を確定します。</summary>
    [Command("fix-deposit")]
    public virtual void FixDeposit() => _cashService.FixDeposit();

    /// <summary>入金を終了します。</summary>
    [Command("end-deposit")]
    public virtual void EndDeposit() => _cashService.EndDeposit();

    /// <summary>出金を実行します。</summary>
    [Command("dispense")]
    public virtual void Dispense(int amount) => _cashService.Dispense(amount);

    /// <summary>デバイスを無効化します。</summary>
    [Command("disable")]
    public virtual void Disable() => _deviceService.Disable();

    /// <summary>デバイスの占有を解除します。</summary>
    [Command("release")]
    public virtual void Release() => _deviceService.Release();

    /// <summary>デバイスをクローズします。</summary>
    [Command("close")]
    public virtual void Close() => _deviceService.Close();

    /// <summary>回収庫の取り外し状態を設定します。</summary>
    [Command("set-box-removed")]
    public virtual void SetBoxRemoved([Argument(Description = "取り外し状態 (true/false)")] bool removed) => _deviceService.SetCollectionBoxRemoved(removed);

    /// <summary>設定を一覧表示または変更します。</summary>
    [Command("config")]
    public virtual void Config() => _console.MarkupLine(_L["messages.usage_config"] ?? "[yellow]Usage: config <list|get|set|save>[/]");

    /// <summary>設定項目を一覧表示します。</summary>
    [Command("config list")]
    public virtual void ConfigList() => _configService.List();

    /// <summary>特定の設定値を取得します。</summary>
    [Command("config get")]
    public virtual void ConfigGet(string key) => _configService.Get(key);

    /// <summary>設定値を一時的に更新します。</summary>
    [Command("config set")]
    public virtual void ConfigSet(string key, string value) => _configService.Set(key, value);

    /// <summary>更新した設定を TOML ファイルに保存します。</summary>
    [Command("config save")]
    public virtual void ConfigSave() => _configService.Save();

    /// <summary>設定をファイルから再読み込みします。</summary>
    [Command("config reload")]
    public virtual void ConfigReload() => _configService.Reload();

    /// <summary>現在の在庫数を調整します。</summary>
    /// <param name="counts">"1000:5,500:10" の形式で指定します。</param>
    [Command("adjust-counts")]
    public virtual void AdjustCashCounts(string counts) => _cashService.AdjustCashCounts(counts);

    /// <summary>取引履歴を CSV 形式でエクスポートします。</summary>
    /// <param name="path">出力先のパス。</param>
    [Command("export-history")]
    public virtual void ExportHistory([Argument(Description = "出力先パス")] string path) => _viewService.ExportHistory(path);

    /// <summary>ログの詳細度を変更します。</summary>
    [Command("log-level")]
    public virtual void LogLevel(string level)
    {
        if (Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(level, true, out _))
        {
            LogProvider.SetLogLevel(level);
            _console.MarkupLine($"[green][[{_L["messages.success_label"]}]][/] {_L["messages.log_level_updated", level]}");
        }
        else
        {
            _console.MarkupLine(_L["messages.invalid_log_level", level]);
        }
    }

    /// <summary>利用可能なコマンドの一覧を表示します。</summary>
    [Command("help")]
    public virtual void Help()
    {
        _console.Write(new Rule($"[cyan]{_L["messages.available_commands"]}[/]").LeftJustified());
        var table = new Table().Border(TableBorder.None);
        table.AddColumn(_L["messages.command_label"] ?? "Command");
        table.AddColumn(_L["messages.description_label"] ?? "Description");

        table.AddRow(Markup.Escape("open"), _L["commands.open"]);
        table.AddRow(Markup.Escape("claim"), _L["commands.claim"]);
        table.AddRow(Markup.Escape("enable"), _L["commands.enable"]);
        table.AddRow(Markup.Escape("status"), _L["commands.status"]);
        table.AddRow(Markup.Escape("read-counts"), _L["commands.read-counts"]);
        table.AddRow(Markup.Escape("deposit [amount]"), _L["commands.deposit"]);
        table.AddRow(Markup.Escape("fix-deposit"), _L["commands.fix-deposit"]);
        table.AddRow(Markup.Escape("end-deposit"), _L["commands.end-deposit"]);
        table.AddRow(Markup.Escape("dispense <amount>"), _L["commands.dispense"]);
        table.AddRow(Markup.Escape("adjust-counts <v:c,v:c>"), _L["commands.adjust-counts"]);
        table.AddRow(Markup.Escape("disable"), _L["commands.disable"]);
        table.AddRow(Markup.Escape("release"), _L["commands.release"]);
        table.AddRow(Markup.Escape("close"), _L["commands.close"]);
        table.AddRow(Markup.Escape("history"), _L["commands.history"]);
        table.AddRow(Markup.Escape("set-box-removed <t|f>"), _L["commands.set-box-removed"]);
        table.AddRow(Markup.Escape("config <list|get|set|save|reload>"), _L["commands.config"]);
        table.AddRow(Markup.Escape("log-level <level>"), _L["commands.log-level"]);
        table.AddRow(Markup.Escape("run-script <path>"), _L["commands.run-script"]);
        table.AddRow(Markup.Escape("help"), Markup.Escape(_L["commands.help"]));
        table.AddRow(Markup.Escape("exit"), Markup.Escape(_L["commands.exit"]));

        _console.Write(table);
    }

    /// <summary>未知のコマンドが入力されたときにメッセージを表示します。</summary>
    /// <param name="command">入力されたコマンド名。</param>
    public virtual void Unknown(string command)
    {
        _console.MarkupLine(_L["messages.unknown_command", command]);
    }
}
