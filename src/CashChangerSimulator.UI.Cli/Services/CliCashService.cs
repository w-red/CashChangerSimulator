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

    public void ReadCashCounts()
    {
        try
        {
            var counts = _changer.ReadCashCounts();
            _console.MarkupLine(_L["CashCountsUpdated"]);

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn(_L["DenominationLabel"]);
            table.AddColumn(new TableColumn(_L["CountLabel"]).RightAligned());
            table.AddColumn(new TableColumn(_L["AmountLabel"]).RightAligned());

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

            table.Caption($"{_L["TotalCaption"]}: {prefix}{_inventory.CalculateTotal(currencyCode):N0}{suffix}");
            _console.Write(table);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Deposit(int? amount)
    {
        try
        {
            if (_options.IsAsync)
            {
                _console.MarkupLine(_L["DepositStarted", amount?.ToString() ?? "All", "True"]);
                _changer.BeginDeposit();
                _console.MarkupLine(_L["DepositAsyncWarning"]);
            }
            else
            {
                _console.MarkupLine(_L["DepositStarted", amount?.ToString() ?? "All", "False"]);
                _changer.BeginDeposit();
                _changer.FixDeposit();
                _changer.EndDeposit(CashDepositAction.Change);
                _console.MarkupLine(_L["DepositCompleted"]);
            }
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void FixDeposit()
    {
        try
        {
            _changer.FixDeposit();
            _console.MarkupLine(_L["DepositFixed"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void EndDeposit()
    {
        try
        {
            _changer.EndDeposit(CashDepositAction.Change);
            _console.MarkupLine(_L["EndDepositCompleted"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Dispense(int amount)
    {
        try
        {
            _changer.DispenseChange(amount);
            _console.MarkupLine(_L["DispensedSuccess", amount]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}
