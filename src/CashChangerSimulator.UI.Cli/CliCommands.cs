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

    public CliCommands(
        SimulatorCashChanger changer,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        TransactionHistory history,
        IScriptExecutionService scriptService,
        CliSessionOptions options,
        IAnsiConsole console)
    {
        _changer = changer;
        _inventory = inventory;
        _metadata = metadata;
        _history = history;
        _scriptService = scriptService;
        _options = options;
        _console = console;
    }

    /// <summary>指定された JSON スクリプトファイルを実行します。</summary>
    [Command("run-script")]
    public async Task RunScript(string path)
    {
        if (!File.Exists(path))
        {
            _console.MarkupLine($"[red]Error: File not found: {path}[/]");
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            _console.MarkupLine($"[cyan]Executing script: {path}...[/]");
            await _scriptService.ExecuteScriptAsync(json);
            _console.MarkupLine("[green]Script execution completed.[/]");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[red]Error executing script: {ex.Message}[/]");
        }
    }

    /// <summary>デバイスの状態と現在の在高を表示します。</summary>
    [Command("status")]
    public void Status()
    {
        _console.Write(new Rule("[cyan]Device Status[/]").LeftJustified());
        _console.MarkupLine($"State: [yellow]{_changer.State}[/]");
        _console.MarkupLine($"Enabled: {(_changer.DeviceEnabled ? "[green]True[/]" : "[red]False[/]")}");
        
        _console.WriteLine();
        _console.Write(new Rule("[cyan]Inventory[/]").LeftJustified());
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Denomination");
        table.AddColumn(new TableColumn("Count").RightAligned());

        foreach (var key in _metadata.SupportedDenominations)
        {
            var count = _inventory.GetCount(key);
            table.AddRow(_metadata.GetDenominationName(key), count.ToString());
        }
        
        var total = _inventory.CalculateTotal(_metadata.CurrencyCode);
        table.Caption($"Total: [bold yellow]{_metadata.SymbolPrefix.CurrentValue}{total:N0}{_metadata.SymbolSuffix.CurrentValue}[/]");
        
        _console.Write(table);
    }

    /// <summary>デバイスを初期化してオープンします。</summary>
    [Command("open")]
    public void Open()
    {
        try {
            _changer.Open();
            _console.MarkupLine("[green]Device opened successfully.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]Failed to open device: {ex.Message}[/]");
        }
    }

    /// <summary>排他的アクセス権を取得します。</summary>
    [Command("claim")]
    public void Claim(int timeout = 1000)
    {
        try {
            _changer.Claim(timeout);
            _console.MarkupLine("[green]Device claimed successfully.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]Failed to claim device: {ex.Message}[/]");
        }
    }

    /// <summary>デバイスを使用可能状態にします。</summary>
    [Command("enable")]
    public void Enable()
    {
        try {
            _changer.DeviceEnabled = true;
            _console.MarkupLine("[green]Device enabled.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]Failed to enable device: {ex.Message}[/]");
        }
    }

    /// <summary>在高を読み取ります。</summary>
    [Command("readCashCounts")]
    public void ReadCashCounts()
    {
        try {
            var counts = _changer.ReadCashCounts();
            _console.MarkupLine("[green]Cash counts updated from device.[/]");
            _console.WriteLine();

            var symbol = _metadata.SymbolPrefix.CurrentValue;
            var suffix = _metadata.SymbolSuffix.CurrentValue;

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Denomination");
            table.AddColumn(new TableColumn("Count").RightAligned());
            table.AddColumn(new TableColumn("Amount").RightAligned());

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
            _console.MarkupLine($"[red]Failed to read cash counts: {ex.Message}[/]");
        }
    }

    /// <summary>入金処理を開始します。</summary>
    [Command("deposit")]
    public void Deposit(int amount)
    {
        try {
            _changer.BeginDeposit();
            _console.MarkupLine($"Depositing [yellow]{amount}[/] (Async: {_options.IsAsync})...");
            
            if (!_options.IsAsync)
            {
                _changer.FixDeposit();
                _changer.EndDeposit(CashDepositAction.Change);
                _console.MarkupLine("[green]Deposit completed.[/]");
            }
            else
            {
                _console.MarkupLine("[yellow]Deposit started in async mode. Use FixDeposit/EndDeposit later.[/]");
            }
        } catch (Exception ex) {
            _console.MarkupLine($"[red]Deposit failed: {ex.Message}[/]");
        }
    }

    /// <summary>投入された現金を確定します。</summary>
    [Command("fixDeposit")]
    public void FixDeposit()
    {
        try {
            _changer.FixDeposit();
            _console.MarkupLine("[green]Deposit fixed.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]FixDeposit failed: {ex.Message}[/]");
        }
    }

    /// <summary>入金処理を終了します。</summary>
    [Command("endDeposit")]
    public void EndDeposit()
    {
        try {
            _changer.EndDeposit(CashDepositAction.Change);
            _console.MarkupLine("[green]EndDeposit completed.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]EndDeposit failed: {ex.Message}[/]");
        }
    }

    /// <summary>出金処理を実行します。</summary>
    [Command("dispense")]
    public void Dispense(int amount)
    {
        try {
            _changer.DispenseChange(amount);
            _console.MarkupLine($"[green]Dispensed {amount} successfully.[/]");
        } catch (Exception ex) {
            _console.MarkupLine($"[red]Dispense failed: {ex.Message}[/]");
        }
    }

    /// <summary>取引履歴を表示します。</summary>
    [Command("history")]
    public void History(int count = 10)
    {
        _console.Write(new Rule($"[cyan]Recent Transactions (up to {count})[/]").LeftJustified());
        var entries = _history.Entries.TakeLast(count).Reverse();
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Timestamp");
        table.AddColumn("Type");
        table.AddColumn(new TableColumn("Amount").RightAligned());
        table.AddColumn("Currency");

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
        _changer.DeviceEnabled = false;
        _console.MarkupLine("[yellow]Device disabled.[/]");
    }

    /// <summary>排他的アクセス権を解放します。</summary>
    [Command("release")]
    public void Release()
    {
        _changer.Release();
        _console.MarkupLine("[green]Device released.[/]");
    }

    /// <summary>デバイスをクローズします。</summary>
    [Command("close")]
    public void Close()
    {
        _changer.Close();
        _console.MarkupLine("[green]Device closed.[/]");
    }

    /// <summary>利用可能なコマンドの一覧を表示します。</summary>
    [Command("help")]
    public void Help()
    {
        _console.Write(new Rule("[cyan]Available commands[/]").LeftJustified());
        
        var table = new Table().NoBorder().HideHeaders();
        table.AddColumn("Command");
        table.AddColumn("Description");

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
        table.AddRow("[yellow]help[/]", "Show this help");
        table.AddRow("[yellow]exit[/]", "Exit the CLI");

        _console.Write(table);
        _console.WriteLine();
    }
}
