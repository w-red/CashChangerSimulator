using Cocona;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using System.IO;
using System.Threading.Tasks;
using CashChangerSimulator.Device.Services;
using Spectre.Console;
using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.UI.Cli;

public partial class CliCommands
{
    private readonly SimulatorCashChanger _changer;
    private readonly Inventory _inventory;
    private readonly ICurrencyMetadataProvider _metadata;
    private readonly TransactionHistory _history;
    private readonly IScriptExecutionService _scriptService;
    private readonly CliSessionOptions _options;
    private readonly IAnsiConsole _console;
    private readonly IStringLocalizer _L;

    public CliCommands(
        SimulatorCashChanger changer,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        TransactionHistory history,
        IScriptExecutionService scriptService,
        CliSessionOptions options,
        IAnsiConsole console,
        IStringLocalizer localizer)
    {
        _changer = changer;
        _inventory = inventory;
        _metadata = metadata;
        _history = history;
        _scriptService = scriptService;
        _options = options;
        _console = console;
        _L = localizer;

        // Subscribing to ErrorEvent for async error reporting
        _changer.ErrorEvent += (sender, e) =>
        {
            _console.WriteLine();
            var hint = GetHint(e.ErrorCode);
            var errMsg = _L["ErrorFormat", "Async Error", (int)e.ErrorCode, e.ErrorCodeExtended, "Async operation failed"];
            _console.MarkupLine(errMsg);
            if (!string.IsNullOrEmpty(hint))
            {
                _console.MarkupLine(_L["HintFormat", hint]);
            }
        };
    }

    private void HandleException(Exception ex)
    {
        if (ex is PosControlException pex)
        {
            var hint = GetHint(pex.ErrorCode);
            var errMsg = _L["ErrorFormat", "Error", (int)pex.ErrorCode, pex.ErrorCodeExtended, pex.Message];
            _console.MarkupLine(errMsg);
            if (!string.IsNullOrEmpty(hint))
            {
                _console.MarkupLine(_L["HintFormat", hint]);
            }
        }
        else
        {
            _console.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
    }

    private string GetHint(ErrorCode errorCode)
    {
        var hintKey = $"ErrorHint_{errorCode}";
        var hint = _L[hintKey];
        if (hint.ResourceNotFound)
        {
            // Fallback for special cases
            if (errorCode == ErrorCode.Illegal && !_changer.DeviceEnabled)
                return _L["ErrorHint_NotEnabled"];
            
            return _L["ErrorHint_Generic"];
        }
        return hint;
    }

    /// <summary>指定された JSON スクリプトファイルを実行します。</summary>
    [Command("run-script")]
    public async Task RunScript(string path)
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
            await _scriptService.ExecuteScriptAsync(json);
            _console.MarkupLine(_L["ScriptCompleted"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    /// <summary>デバイスの状態と現在の在高を表示します。</summary>
    [Command("status")]
    public void Status()
    {
        _console.Write(new Rule($"[cyan]{_L["StatusHeader"]}[/]").LeftJustified());
        _console.MarkupLine($"{_L["StateLabel"]}: [yellow]{_changer.State}[/]");
        _console.MarkupLine($"{_L["EnabledLabel"]}: {(_changer.DeviceEnabled ? "[green]True[/]" : "[red]False[/]")}");
        
        _console.WriteLine();
        _console.Write(new Rule($"[cyan]{_L["InventoryHeader"]}[/]").LeftJustified());
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(_L["DenominationLabel"]);
        table.AddColumn(new TableColumn(_L["CountLabel"]).RightAligned());

        foreach (var key in _metadata.SupportedDenominations)
        {
            var count = _inventory.GetCount(key);
            table.AddRow(_metadata.GetDenominationName(key), count.ToString());
        }
        
        var total = _inventory.CalculateTotal(_metadata.CurrencyCode);
        table.Caption($"{_L["TotalCaption"]}: [bold yellow]{_metadata.SymbolPrefix.CurrentValue}{total:N0}{_metadata.SymbolSuffix.CurrentValue}[/]");
        
        _console.Write(table);
    }

    /// <summary>デバイスを初期化してオープンします。</summary>
    [Command("open")]
    public void Open()
    {
        try {
            _changer.Open();
            _console.MarkupLine($"[green]{_L["DeviceOpened"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>排他的アクセス権を取得します。</summary>
    [Command("claim")]
    public void Claim(int timeout = 1000)
    {
        try {
            _changer.Claim(timeout);
            _console.MarkupLine($"[green]{_L["DeviceClaimed"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>デバイスを使用可能状態にします。</summary>
    [Command("enable")]
    public void Enable()
    {
        try {
            _changer.DeviceEnabled = true;
            _console.MarkupLine($"[green]{_L["DeviceEnabled"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>在高を読み取ります。</summary>
    [Command("readCashCounts")]
    public void ReadCashCounts()
    {
        try {
            var counts = _changer.ReadCashCounts();
            _console.MarkupLine($"[green]{_L["CashCountsUpdated"]}[/]");
            _console.WriteLine();

            var symbol = _metadata.SymbolPrefix.CurrentValue;
            var suffix = _metadata.SymbolSuffix.CurrentValue;

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(_L["DenominationLabel"]);
            table.AddColumn(new TableColumn(_L["CountLabel"]).RightAligned());
            table.AddColumn(new TableColumn(_L["AmountLabel"]).RightAligned());

            foreach (var cc in counts.Counts)
            {
                var key = _metadata.SupportedDenominations.FirstOrDefault(k => k.Type == (CashType)cc.Type && k.Value == cc.NominalValue);
                var name = key != null ? _metadata.GetDenominationName(key) : cc.NominalValue.ToString();
                var amount = cc.NominalValue * cc.Count;

                table.AddRow(name, cc.Count.ToString(), $"{symbol}{amount:N0}{suffix}");
            }
            
            var total = _inventory.CalculateTotal(_metadata.CurrencyCode);
            table.Columns[2].Footer($"{symbol}{total:N0}{suffix}");
            
            _console.Write(table);

        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>入金処理を開始します。</summary>
    [Command("deposit")]
    public void Deposit(int amount)
    {
        try {
            _changer.BeginDeposit();
            _console.MarkupLine(_L["DepositStarted", amount, _options.IsAsync]);
            
            if (!_options.IsAsync)
            {
                _changer.FixDeposit();
                _changer.EndDeposit(CashDepositAction.Change);
                _console.MarkupLine($"[green]{_L["DepositCompleted"]}[/]");
            }
            else
            {
                _console.MarkupLine($"[yellow]{_L["DepositAsyncWarning"]}[/]");
            }
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>投入された現金を確定します。</summary>
    [Command("fixDeposit")]
    public void FixDeposit()
    {
        try {
            _changer.FixDeposit();
            _console.MarkupLine($"[green]{_L["DepositFixed"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>入金処理を終了します。</summary>
    [Command("endDeposit")]
    public void EndDeposit()
    {
        try {
            _changer.EndDeposit(CashDepositAction.Change);
            _console.MarkupLine($"[green]{_L["EndDepositCompleted"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>出金処理を実行します。</summary>
    [Command("dispense")]
    public void Dispense(int amount)
    {
        try {
            _changer.DispenseChange(amount);
            _console.MarkupLine($"[green]{_L["DispensedSuccess", amount]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>取引履歴を表示します。</summary>
    [Command("history")]
    public void History(int count = 10)
    {
        _console.Write(new Rule($"[cyan]{_L["TransactionHistoryHeader", count]}[/]").LeftJustified());
        var entries = _history.Entries.TakeLast(count).Reverse();
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(_L["TimestampLabel"]);
        table.AddColumn(_L["TypeLabel"]);
        table.AddColumn(new TableColumn(_L["AmountLabel"]).RightAligned());
        table.AddColumn(_L["CurrencyLabel"]);

        foreach (var entry in entries)
        {
            table.AddRow(
                entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.Type.ToString(),
                entry.Amount.ToString("N0"),
                _metadata.CurrencyCode
            );
        }
        _console.Write(table);
    }

    /// <summary>デバイスを無効化します。</summary>
    [Command("disable")]
    public void Disable()
    {
        try {
            _changer.DeviceEnabled = false;
            _console.MarkupLine($"[yellow]{_L["DeviceDisabled"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>排他的アクセス権を解放します。</summary>
    [Command("release")]
    public void Release()
    {
        try {
            _changer.Release();
            _console.MarkupLine($"[yellow]{_L["DeviceReleased"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>デバイスをクローズします。</summary>
    [Command("close")]
    public void Close()
    {
        try {
            _changer.Close();
            _console.MarkupLine($"[yellow]{_L["DeviceClosed"]}[/]");
        } catch (Exception ex) {
            HandleException(ex);
        }
    }

    /// <summary>利用可能なコマンドの一覧を表示します。</summary>
    [Command("help")]
    public void Help()
    {
        _console.Write(new Rule($"[cyan]{_L["AvailableCommands"]}[/]").LeftJustified());
        
        var table = new Table().NoBorder().HideHeaders();
        table.AddColumn(_L["CommandLabel"]);
        table.AddColumn(_L["DescriptionLabel"]);

        table.AddRow("[yellow]open[/]", "Open the device");
        table.AddRow("[yellow]claim [[timeout]][/]", "Claim exclusive access (default: 1000ms)");
        table.AddRow("[yellow]enable[/]", "Enable the device");
        table.AddRow("[yellow]status[/]", "Show device status and inventory");
        table.AddRow("[yellow]readCashCounts[/]", "Read cash counts from device");
        table.AddRow("[yellow]deposit <amount>[/]", "Begin deposit");
        table.AddRow("[yellow]fixDeposit[/]", "Fix current deposit (Async mode)");
        table.AddRow("[yellow]endDeposit[/]", "End deposit and dispense change (Async mode)");
        table.AddRow("[yellow]dispense <amount>[/]", "Dispense change");
        table.AddRow("[yellow]history [[count]][/]", "Show transaction history (default: 10)");
        table.AddRow("[yellow]disable[/]", "Disable the device");
        table.AddRow("[yellow]release[/]", "Release exclusive access");
        table.AddRow("[yellow]close[/]", "Close the device");
        table.AddRow("[yellow]run-script <path>[/]", "Execute a JSON scenario script");
        table.AddRow("[yellow]help[/]", _L["HelpDescription"].Value);
        table.AddRow("[yellow]exit[/]", _L["ExitDescription"].Value);

        _console.Write(table);
        _console.WriteLine();
    }
}
