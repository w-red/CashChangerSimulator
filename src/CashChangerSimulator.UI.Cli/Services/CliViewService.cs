using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliViewService : CliServiceBase
{
    private readonly SimulatorCashChanger _changer;
    private readonly Inventory _inventory;
    private readonly ICurrencyMetadataProvider _metadata;
    private readonly TransactionHistory _history;

    public CliViewService(
        SimulatorCashChanger changer,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        TransactionHistory history,
        IAnsiConsole console,
        IStringLocalizer localizer) : base(console, localizer)
    {
        _changer = changer;
        _inventory = inventory;
        _metadata = metadata;
        _history = history;
    }

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
            table.AddRow(key.ToDenominationString(), count.ToString());
        }

        var currencyCode = _metadata.CurrencyCode;
        var prefix = _metadata.SymbolPrefix.CurrentValue;
        var suffix = _metadata.SymbolSuffix.CurrentValue;
        table.Caption($"{_L["TotalCaption"]}: {prefix}{_inventory.CalculateTotal(currencyCode):N0}{suffix}");
        
        _console.Write(table);
    }

    public void History(int count)
    {
        _console.Write(new Rule($"[cyan]{_L["TransactionHistoryHeader", count]}[/]").LeftJustified());
        
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(_L["TimestampLabel"]);
        table.AddColumn(_L["TypeLabel"]);
        table.AddColumn(new TableColumn(_L["AmountLabel"]).RightAligned());
        table.AddColumn(_L["CurrencyLabel"]);

        var entries = _history.Entries.TakeLast(count).Reverse();
        foreach (var entry in entries)
        {
            table.AddRow(
                entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.Type.ToString(),
                entry.Amount.ToString("N0"),
                _metadata.CurrencyCode ?? "-"
            );
        }

        _console.Write(table);
    }
}
