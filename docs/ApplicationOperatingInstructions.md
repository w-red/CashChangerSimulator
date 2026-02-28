# Standard Mode Operating Instructions

Standard Mode is designed for direct manual interaction with the simulated device via the GUI.

## 1. Screen Layout

- **Inventory Screen**: Displays current denomination counts.
- **Deposit/Dispense Screen**: Perform manual deposit and dispense simulations.
- **Activity Feed**: A chronological list of device events (e.g., DataEvent, StatusUpdateEvent).

![Main Dashboard](images/main_dashboard.png)
*Fig: Simulator Main Dashboard*

## 2. Inventory Management

1. **Initial State**: Inventory is initialized based on `config.toml` at startup.
2. **Manual Updates**: You can directly edit machine counts in the Inventory tab and click "Save" or "Apply" to update the simulator's logic immediately.
3. **Discrepancy Toggle**: Use the "Set Discrepancy" option in the simulation menu to simulate an inventory mismatch state.

## 3. Manual Deposit (Deposit)

![Deposit Window](images/deposit_window.png)

1. Select the `Deposit` tab.
2. Click "Begin Deposit".
3. Select numbers or input counts, then click "Insert Cash" to feed the device.
4. Click "End Deposit" to finalize; change will be dispensed if applicable.

## 4. Manual Dispense (Dispense)

![Dispense Window](images/dispense_window.png)

1. Select the `Dispense` tab.
2. **Amount Dispense (`DispenseChange`)**: Enter the total amount and click "Execute". The system automatically calculates the best denomination combination.
3. **Inventory Dispense (`DispenseCash`)**: Input specific counts for each denomination and click "Dispense Cash".

## 5. Log Monitoring

- Real-time behavior is visible in the **Activity Feed**.
- Detailed technical logs are stored in the `logs/` directory of the application's root folder.

## 6. Configuration File (config.toml)

You can customize the simulator behavior by editing `config.toml` located in the application's working directory. The file is auto-generated with default values on first launch.

### `[System]` — General Settings

| Key            | Type   | Default      | Description                             |
| -------------- | ------ | ------------ | --------------------------------------- |
| `CurrencyCode` | string | `"JPY"`      | Active currency code (ISO 4217)         |
| `CultureCode`  | string | `"en-US"`    | UI locale setting                       |
| `UIMode`       | string | `"Standard"` | UI mode (`Standard` / `PosTransaction`) |

### `[Inventory.<CurrencyCode>.Denominations.<DenomKey>]` — Inventory Settings

Per-currency denomination configuration. Denomination keys use `B` (bill) or `C` (coin) prefix followed by the face value (e.g., `B1000`, `C100`).

| Key            | Type   | Default | Description                                     |
| -------------- | ------ | ------- | ----------------------------------------------- |
| `DisplayName`  | string | —       | Display name shown in the UI (e.g., `"1000円"`) |
| `InitialCount` | int    | `0`     | Initial count at startup                        |
| `NearEmpty`    | int    | `5`     | NearEmpty threshold (at or below this count)    |
| `NearFull`     | int    | `90`    | NearFull threshold (at or above this count)     |
| `Full`         | int    | `100`   | Full threshold (at or above this count)         |

### `[Thresholds]` — Default Thresholds

Global threshold values applied when no per-denomination override is set.

| Key         | Type | Default | Description         |
| ----------- | ---- | ------- | ------------------- |
| `NearEmpty` | int  | `5`     | NearEmpty threshold |
| `NearFull`  | int  | `90`    | NearFull threshold  |
| `Full`      | int  | `100`   | Full threshold      |

### `[Logging]` — Log Settings

| Key             | Type   | Default         | Description                                            |
| --------------- | ------ | --------------- | ------------------------------------------------------ |
| `EnableConsole` | bool   | `true`          | Enable/disable console output                          |
| `EnableFile`    | bool   | `true`          | Enable/disable file output                             |
| `LogLevel`      | string | `"Information"` | Log level (`Debug`, `Information`, `Warning`, `Error`) |
| `LogDirectory`  | string | `"logs"`        | Log output directory                                   |
| `LogFileName`   | string | `"app.log"`     | Log file name                                          |

### `[Simulation]` — Simulation Behavior

| Key               | Type | Default | Description                                    |
| ----------------- | ---- | ------- | ---------------------------------------------- |
| `DispenseDelayMs` | int  | `500`   | Dispense operation delay in milliseconds (≥ 0) |

### Example Configuration

```toml
[System]
CurrencyCode = "JPY"
CultureCode = "ja-JP"
UIMode = "Standard"

[Inventory.JPY.Denominations.B1000]
DisplayName = "1000円"
InitialCount = 50
NearEmpty = 5
NearFull = 90
Full = 100

[Thresholds]
NearEmpty = 5
NearFull = 90
Full = 100

[Logging]
EnableConsole = true
EnableFile = true
LogLevel = "Information"
LogDirectory = "logs"
LogFileName = "app.log"

[Simulation]
DispenseDelayMs = 500
```

---
*For the Japanese version, see [ApplicationOperatingInstructions_JP.md](ApplicationOperatingInstructions_JP.md).*
