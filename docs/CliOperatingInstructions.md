# CLI Operating Instructions

The CLI UI provides a command-line interface for interacting with and managing the CashChanger simulator. It is optimized for automated testing and batch operations.

## 1. Getting Started

Run the following command from the repository root:

```powershell
dotnet run --project src/CashChangerSimulator.UI.Cli
```

### Startup Options
- `--currency <CODE>`: Overrides the currency setting (e.g., `USD`) on startup.

## 2. Available Commands

After startup, enter the following commands:

| Command             | Description                                              |
| :------------------ | :------------------------------------------------------- |
| `status`            | Shows device state and inventory summary.                |
| `open`              | Opens (initializes) the device.                          |
| `claim [timeout]`   | Claims exclusive access. Default timeout is 1000ms.      |
| `enable`            | Enables the device.                                      |
| `readCashCounts`    | Reads detailed cash counts and displays them in a table. |
| `deposit <amount>`  | Simulates a deposit operation.                           |
| `dispense <amount>` | Executes a dispense operation.                           |
| `history [count]`   | Shows recent transaction history (default 10).           |
| `run-script <path>` | Executes a series of operations from a JSON file.        |
| `disable`           | Disables the device.                                     |
| `release`           | Releases exclusive access.                               |
| `close`             | Closes the device.                                       |
| `exit` / `quit`     | Closes the CLI application.                              |

## 3. Scripting (run-script)

You can automate complex operations using JSON script files.

### Sample Script (`sample.json`)

```json
[
  { "Op": "BeginDeposit" },
  { "Op": "TrackDeposit", "Currency": "JPY", "Type": "Bill", "Value": 1000, "Count": 1 },
  { "Op": "FixDeposit" },
  { "Op": "EndDeposit", "Action": "Store" },
  { "Op": "Delay", "Value": 500 },
  { "Op": "Dispense", "Value": 1000 }
]
```

### Execution
```text
> run-script sample.json
```

## 4. Logging

Detailed logs are stored in the `logs/` folder of the execution directory (shared with WPF UI).
