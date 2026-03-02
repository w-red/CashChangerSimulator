using ConsoleAppFramework;
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
using Kokuban;

namespace CashChangerSimulator.UI.Cli;

public partial class CliCommands
{
    private readonly SimulatorCashChanger _changer;
    private readonly Inventory _inventory;
    private readonly ICurrencyMetadataProvider _metadata;
    private readonly TransactionHistory _history;
    private readonly IScriptExecutionService _scriptService;

    public CliCommands(
        SimulatorCashChanger changer,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        TransactionHistory history,
        IScriptExecutionService scriptService)
    {
        _changer = changer;
        _inventory = inventory;
        _metadata = metadata;
        _history = history;
        _scriptService = scriptService;
    }

    /// <summary>指定された JSON スクリプトファイルを実行します。</summary>
    [Command("run-script")]
    public async Task RunScript(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine(Chalk.Red[$"Error: File not found: {path}"]);
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            Console.WriteLine(Chalk.Cyan[$"Executing script: {path}..."]);
            await _scriptService.ExecuteScriptAsync(json);
            Console.WriteLine(Chalk.Green["Script execution completed."]);
        }
        catch (Exception ex)
        {
            Console.WriteLine(Chalk.Red[$"Error executing script: {ex.Message}"]);
        }
    }

    /// <summary>デバイスの状態と現在の在高を表示します。</summary>
    [Command("status")]
    public void Status()
    {
        Console.WriteLine(Chalk.Cyan.Bold["--- Device Status ---"]);
        Console.WriteLine($"State: {Chalk.Yellow[_changer.State.ToString()]}");
        Console.WriteLine($"Enabled: {(_changer.DeviceEnabled ? Chalk.Green["True"] : Chalk.Red["False"])}");
        Console.WriteLine();
        Console.WriteLine(Chalk.Cyan.Bold["--- Inventory ---"]);
        foreach (var key in _metadata.SupportedDenominations)
        {
            var count = _inventory.GetCount(key);
            Console.WriteLine($"{_metadata.GetDenominationName(key)}: {count}");
        }
        var total = _inventory.CalculateTotal(_metadata.CurrencyCode);
        Console.WriteLine($"{Chalk.Yellow.Bold["Total"]}: {Chalk.Yellow.Bold[$"{_metadata.SymbolPrefix.CurrentValue}{total:N0}{_metadata.SymbolSuffix.CurrentValue}"]}");
    }

    /// <summary>デバイスを初期化してオープンします。</summary>
    [Command("open")]
    public void Open()
    {
        try {
            _changer.Open();
            Console.WriteLine(Chalk.Green["Device opened successfully."]);
        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Failed to open device: {ex.Message}"]);
        }
    }

    /// <summary>排他的アクセス権を取得します。</summary>
    [Command("claim")]
    public void Claim(int timeout = 1000)
    {
        try {
            _changer.Claim(timeout);
            Console.WriteLine(Chalk.Green["Device claimed successfully."]);
        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Failed to claim device: {ex.Message}"]);
        }
    }

    /// <summary>デバイスを使用可能状態にします。</summary>
    [Command("enable")]
    public void Enable()
    {
        try {
            _changer.DeviceEnabled = true;
            Console.WriteLine(Chalk.Green["Device enabled."]);
        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Failed to enable device: {ex.Message}"]);
        }
    }

    /// <summary>在高を読み取ります。</summary>
    [Command("readCashCounts")]
    public void ReadCashCounts()
    {
        try {
            var counts = _changer.ReadCashCounts();
            Console.WriteLine(Chalk.Green["Cash counts updated from device."]);
            Console.WriteLine();
            var symbol = _metadata.SymbolPrefix.CurrentValue;
            var suffix = _metadata.SymbolSuffix.CurrentValue;

            // Header: Cyan and Bold
            Console.WriteLine(Chalk.Cyan.Bold[$"{"Denomination",-20} | {"Count",6} | {"Amount",14}"]);
            Console.WriteLine(Chalk.Gray[new string('─', 21) + "┼" + new string('─', 8) + "┼" + new string('─', 16)]);

            foreach (var cc in counts.Counts)
            {
                // Find matching DenominationKey to get DisplayName from TOML
                var key = _metadata.SupportedDenominations.FirstOrDefault(k => k.Type == (CashType)cc.Type && k.Value == cc.NominalValue);
                var name = key != null ? _metadata.GetDenominationName(key) : cc.NominalValue.ToString();
                var amount = cc.NominalValue * cc.Count;

                Console.WriteLine($"{name,-20} | {cc.Count,6} | {symbol}{amount,12:N0}{suffix}");
            }
            
            var total = _inventory.CalculateTotal(_metadata.CurrencyCode);
            Console.WriteLine(Chalk.Gray[new string('─', 21) + "┼" + new string('─', 8) + "┼" + new string('─', 16)]);
            // Total: Yellow and Bold
            Console.WriteLine(Chalk.Yellow.Bold[$"{"Total",-20} | {"",6} | {symbol}{total,12:N0}{suffix}"]);

        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Failed to read cash counts: {ex.Message}"]);
        }
    }

    /// <summary>入金処理を開始します。</summary>
    [Command("deposit")]
    public void Deposit(int amount)
    {
        try {
            _changer.BeginDeposit();
            Console.WriteLine($"Depositing {Chalk.Yellow[amount.ToString()]}...");
            // Simulator workaround: In real UPOS, deposit happens hardware-side.
            // Here we can use FixDeposit or just let the simulator handle it.
            _changer.FixDeposit();
            _changer.EndDeposit(CashDepositAction.Change);
            Console.WriteLine(Chalk.Green["Deposit completed."]);
        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Deposit failed: {ex.Message}"]);
        }
    }

    /// <summary>出金処理を実行します。</summary>
    [Command("dispense")]
    public void Dispense(int amount)
    {
        try {
            _changer.DispenseChange(amount);
            Console.WriteLine(Chalk.Green[$"Dispensed {amount} successfully."]);
        } catch (Exception ex) {
            Console.WriteLine(Chalk.Red[$"Dispense failed: {ex.Message}"]);
        }
    }

    /// <summary>取引履歴を表示します。</summary>
    [Command("history")]
    public void History(int count = 10)
    {
        Console.WriteLine($"--- Recent Transactions (up to {count}) ---");
        var entries = _history.Entries.TakeLast(count).Reverse();
        foreach (var entry in entries)
        {
            Console.WriteLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} | {entry.Type,-10} | {entry.Amount,8:N0} {_metadata.CurrencyCode}");
        }
    }

    /// <summary>デバイスを無効化します。</summary>
    [Command("disable")]
    public void Disable()
    {
        _changer.DeviceEnabled = false;
        Console.WriteLine(Chalk.Yellow["Device disabled."]);
    }

    /// <summary>排他的アクセス権を解放します。</summary>
    [Command("release")]
    public void Release()
    {
        _changer.Release();
        Console.WriteLine(Chalk.Green["Device released."]);
    }

    /// <summary>デバイスをクローズします。</summary>
    [Command("close")]
    public void Close()
    {
        _changer.Close();
        Console.WriteLine(Chalk.Green["Device closed."]);
    }
}
