using System.Threading.Tasks;

namespace CashChangerSimulator.UI.Cli.Services;

/// <summary><see cref="ICliCommandDispatcher"/> の実装クラス。</summary>
public class CliCommandDispatcher(CliCommands commands) : ICliCommandDispatcher
{
    private readonly CliCommands _commands = commands;

    /// <inheritdoc/>
    public Task DispatchAsync(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return Task.CompletedTask;

        var parts = line.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return Task.CompletedTask;

        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "open":
                _commands.Open();
                break;
            case "claim":
                var timeout = parts.Length > 1 && int.TryParse(parts[1], out var t) ? t : 1000;
                _commands.Claim(timeout);
                break;
            case "enable":
                _commands.Enable();
                break;
            case "disable":
                _commands.Disable();
                break;
            case "status":
                _commands.Status();
                break;
            case "read-counts":
                _commands.ReadCashCounts();
                break;
            case "deposit":
                if (parts.Length > 1 && int.TryParse(parts[1], out var amount))
                    _commands.Deposit(amount);
                else
                    _commands.Deposit(null);
                break;
            case "fix-deposit":
                _commands.FixDeposit();
                break;
            case "end-deposit":
                _commands.EndDeposit();
                break;
            case "adjust-counts":
                if (parts.Length > 1)
                    _commands.AdjustCashCounts(parts[1]);
                break;
            case "dispense":
                if (parts.Length > 1 && int.TryParse(parts[1], out var dispAmt))
                    _commands.Dispense(dispAmt);
                break;
            case "history":
                var count = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 10;
                _commands.History(count);
                break;
            case "release":
                _commands.Release();
                break;
            case "close":
                _commands.Close();
                break;
            case "run-script":
                if (parts.Length > 1)
                    return _commands.RunScript(parts[1]);
                break;
            case "config":
                HandleConfig(parts);
                break;
            case "log-level":
                if (parts.Length > 1)
                    _commands.LogLevel(parts[1]);
                break;
            case "set-box-removed":
                if (parts.Length > 1 && bool.TryParse(parts[1], out var removed))
                    _commands.SetBoxRemoved(removed);
                break;
            case "help":
                _commands.Help();
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleConfig(string[] parts)
    {
        if (parts.Length > 1)
        {
            var sub = parts[1].ToLowerInvariant();
            switch (sub)
            {
                case "list": _commands.ConfigList(); break;
                case "get": if (parts.Length > 2) _commands.ConfigGet(parts[2]); break;
                case "set": if (parts.Length > 3) _commands.ConfigSet(parts[2], parts[3]); break;
                case "save": _commands.ConfigSave(); break;
                case "reload": _commands.ConfigReload(); break;
                default: _commands.Config(); break;
            }
        }
        else
        {
            _commands.Config();
        }
    }
}
