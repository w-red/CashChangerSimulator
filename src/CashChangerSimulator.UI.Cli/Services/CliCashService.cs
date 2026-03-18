using Microsoft.PointOfService;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliCashService : CliServiceBase
{
    private readonly SimulatorCashChanger _changer;
    private readonly Inventory _inventory;
    private readonly ICurrencyMetadataProvider _metadata;
    private readonly CliSessionOptions _options;

    public CliCashService(
        SimulatorCashChanger changer,
        Inventory inventory,
        ICurrencyMetadataProvider metadata,
        CliSessionOptions options,
        IAnsiConsole console,
        IStringLocalizer localizer) : base(console, localizer)
    {
        _changer = changer;
        _inventory = inventory;
        _metadata = metadata;
        _options = options;
    }

    public virtual void ReadCashCounts()
    {
        try
        {
            var counts = _changer.ReadCashCounts();
            ReportSuccess(_L["messages.cash_counts_updated"]);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(_L["messages.denomination_label"]);
            table.AddColumn(new TableColumn(_L["messages.count_label"]).RightAligned());
            table.AddColumn(new TableColumn(_L["messages.amount_label"]).RightAligned());

            var currencyCode = _metadata.CurrencyCode;
            var prefix = _metadata.SymbolPrefix.CurrentValue;
            var suffix = _metadata.SymbolSuffix.CurrentValue;

            foreach (var key in _metadata.SupportedDenominations)
            {
                var cc = counts.Counts.FirstOrDefault(c => c.NominalValue == (int)key.Value && (int)c.Type == (int)key.Type);
                var count = cc.Count; // CashCount is a struct; default is Count=0
                var amount = key.Value * count;
                table.AddRow(
                    key.ToDenominationString(),
                    count.ToString(),
                    $"{prefix}{amount:N0}{suffix}"
                );
            }

            table.Caption($"{_L["messages.total_caption"]}: {prefix}{_inventory.CalculateTotal(currencyCode):N0}{suffix}");
            _console.Write(table);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
    
    public virtual void ShowDepositTray()
    {
        var escrow = _inventory.EscrowCounts.ToList();
        if (!escrow.Any() && !_changer.IsDepositInProgress) return;

        var table = new Table().Border(TableBorder.Rounded).BorderColor(Color.Yellow);
        table.Title($"[yellow]{_L["messages.deposit_tray_label"]}[/]");
        table.AddColumn(_L["messages.denomination_label"]);
        table.AddColumn(new TableColumn(_L["messages.count_label"]).RightAligned());
        table.AddColumn(new TableColumn(_L["messages.amount_label"]).RightAligned());

        var prefix = _metadata.SymbolPrefix.CurrentValue;
        var suffix = _metadata.SymbolSuffix.CurrentValue;
        decimal trayTotal = 0;

        foreach (var key in _metadata.SupportedDenominations)
        {
            var count = escrow.FirstOrDefault(e => e.Key.Value == key.Value && e.Key.Type == key.Type).Value;
            if (count > 0)
            {
                var amount = key.Value * count;
                trayTotal += amount;
                table.AddRow(
                    key.ToDenominationString(),
                    count.ToString(),
                    $"{prefix}{amount:N0}{suffix}"
                );
            }
        }

        var required = _changer.RequiredAmount;
        var remaining = Math.Max(0, required - trayTotal);
        
        table.Caption($"{_L["messages.total_caption"]}: {prefix}{trayTotal:N0}{suffix} / {_L["messages.required_amount_label"]}: {prefix}{required:N0}{suffix} ([red]{_L["messages.remaining_label"]}: {prefix}{remaining:N0}{suffix}[/])");
        _console.Write(table);
    }

    public virtual void Deposit(int? amount)
    {
        try
        {
            if (_options.IsAsync)
            {
                _console.MarkupLine(_L["messages.deposit_started", amount?.ToString() ?? "All", "True"]);
                if (amount.HasValue) _changer.RequiredAmount = amount.Value;
                _changer.BeginDeposit();
                _console.MarkupLine(_L["messages.deposit_async_warning"]);
                ShowDepositTray();
            }
            else
            {
                _console.MarkupLine(_L["messages.deposit_started", amount?.ToString() ?? "All", "False"]);
                if (amount.HasValue) _changer.RequiredAmount = amount.Value;
                _changer.BeginDeposit();
                _changer.FixDeposit();
                ShowDepositTray();
                _changer.EndDeposit(CashDepositAction.Change);
                ReportSuccess(_L["messages.deposit_completed"]);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void FixDeposit()
    {
        try
        {
            _changer.FixDeposit();
            ShowDepositTray();
            ReportSuccess(_L["messages.deposit_fixed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void EndDeposit()
    {
        try
        {
            ShowDepositTray();
            _changer.EndDeposit(CashDepositAction.Change);
            ReportSuccess(_L["messages.end_deposit_completed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void AdjustCashCounts(string input)
    {
        try
        {
            var currencyCode = _options.CurrencyCode;
            var factor = UposCurrencyHelper.GetCurrencyFactor(currencyCode);
            var availableKeys = _metadata.SupportedDenominations
                .Select(d => new DenominationKey(d.Value, d.Type, currencyCode));

            var counts = CashCountAdapter.ParseCashCounts(input, currencyCode, factor, availableKeys);

            if (counts.Any())
            {
                _changer.AdjustCashCounts(counts);
                ReportSuccess(_L["messages.adjust_cash_counts_success", input]);
            }
            else
            {
                _console.MarkupLine(_L["messages.invalid_adjust_format"]);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public virtual void Dispense(int amount)
    {
        try
        {
            _changer.DispenseChange(amount);
            ReportSuccess(_L["messages.dispensed_success", amount]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}
