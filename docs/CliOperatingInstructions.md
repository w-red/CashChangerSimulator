# CLI Operating Instructions

The CLI UI provides a command-line interface for interacting with and managing the CashChanger simulator. It is optimized for automated testing, batch operations, and detailed debugging.

## 1. Getting Started

Run the following command from the repository root:

```powershell
dotnet run --project src/CashChangerSimulator.UI.Cli
```

## 2. Basic Operation Flow

The simulator follows the standard UPOS lifecycle (Open → Claim → Enable):
1. `open`: Initializes the device.
2. `claim`: Acquires exclusive access (Claimed state).
3. `enable`: Enables the device for operations (Enabled state).
4. (Perform operations: deposit, dispense, inventory check, etc.)
5. `disable`: Disables the device.
6. `release`: Releases exclusive access.
7. `close`: Closes the device.

## 3. Available Commands

After startup, enter the following commands to interact with the device.

### General & Diagnostics
| Command           | Description                                               |
| :---------------- | :-------------------------------------------------------- |
| `status`          | Shows current device state (State) and inventory summary. |
| `history [count]` | Shows recent transaction history (default 10).            |
| `help`            | Displays help for all available commands.                 |
| `exit` / `quit`   | Closes the CLI application.                               |

### Device Control
| Command              | Description                               |
| :------------------- | :---------------------------------------- |
| `open` / `close`     | Opens or closes the device.               |
| `claim [timeout]`    | Claims exclusive access (default 5000ms). |
| `release`            | Releases exclusive access.                |
| `enable` / `disable` | Enables or disables the device.           |

### Cash Operations
| Command                   | Description                                                                         |
| :------------------------ | :---------------------------------------------------------------------------------- |
| `read-counts`             | Displays detailed inventory counts in a table.                                      |
| `deposit [amount]`        | Starts a deposit. If amount is specified, denominations are inserted automatically. |
| `fix-deposit`             | Validates inserted cash and updates the inventory.                                  |
| `end-deposit`             | Finalizes the deposit transaction.                                                  |
| `dispense <amount>`       | Dispenses the specified total amount.                                               |
| `adjust-counts <v:c,v:c>` | Overwrites inventory counts (e.g., `1000:10,500:5`).                                |

### Configuration & System
| Command              | Description                                                     |
| :------------------- | :-------------------------------------------------------------- |
| `config list`        | Lists all configuration items and their values.                 |
| `config set <k> <v>` | Temporarily updates a configuration value.                      |
| `config save`        | Saves current configurations to `config.toml`.                  |
| `log-level <level>`  | Changes the log level (debug, info, warning, error) at runtime. |
| `run-script <path>`  | Executes a series of operations from a JSON file.               |

## 4. Scripting (run-script)

You can automate complex scenarios using JSON script files.

### Sample Script (`sample.json`)

```json
[
  { "Op": "Open" },
  { "Op": "Claim", "Value": 1000 },
  { "Op": "Enable" },
  { "Op": "BeginDeposit" },
  { "Op": "TrackDeposit", "Currency": "JPY", "Type": "Bill", "Value": 1000, "Count": 1 },
  { "Op": "FixDeposit" },
  { "Op": "EndDeposit" },
  { "Op": "Dispense", "Value": 1000 }
]
```

## 5. Logging
Detailed logs are stored in the `logs/` folder. You can adjust the output verbosity in real-time using the `log-level` command.
