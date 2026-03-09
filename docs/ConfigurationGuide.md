# Configuration Guide

The Cash Changer Simulator can be highly customized through the `config.toml` file. In the WPF application, many of these settings can also be modified via the graphical user interface.

## 1. Configuration File Location
The application refers to `config.toml` in the same directory as the executable. If the file does not exist, it will be automatically generated with default values.

## 2. System Settings (`[System]`)
Basic application behavior.

| Key            | Type   | Default      | Description                                |
| :------------- | :----- | :----------- | :----------------------------------------- |
| `CurrencyCode` | string | `"JPY"`      | Active currency code (e.g., `JPY`, `USD`). |
| `CultureCode`  | string | `"en-US"`    | Display language (`ja-JP`, `en-US`).       |
| `UIMode`       | string | `"Standard"` | UI behavior mode (`Standard`, `Pos`).      |

## 3. Simulation Settings (`[Simulation]`)
Adjusting the device's simulated behavior.

| Key               | Type | Default | Description                                       |
| :---------------- | :--- | :------ | :------------------------------------------------ |
| `DispenseDelayMs` | int  | `500`   | Simulated delay for dispense operations (ms).     |
| `HotStart`        | bool | `false` | Automatically `Open` the device on app startup.   |
| `CapRealTimeData` | bool | `true`  | Capability flag for real-time data notifications. |

## 4. Logging Settings (`[Logging]`)
Controls diagnostic output.

| Key             | Type   | Default         | Description                                                |
| :-------------- | :----- | :-------------- | :--------------------------------------------------------- |
| `EnableConsole` | bool   | `true`          | Output logs to console.                                    |
| `EnableFile`    | bool   | `true`          | Output logs to file.                                       |
| `LogLevel`      | string | `"Information"` | Output level (`Debug`, `Information`, `Warning`, `Error`). |
| `LogDirectory`  | string | `"logs"`        | Directory name for log storage.                            |
| `LogFileName`   | string | `"app.log"`     | Log file name.                                             |

## 5. Inventory & Thresholds

### Global Thresholds (`[Thresholds]`)
Default values used when specific denominations do not have their own overrides.

| Key         | Type | Default | Description                                        |
| :---------- | :--- | :------ | :------------------------------------------------- |
| `NearEmpty` | int  | `5`     | Count at or below which "Near Empty" is triggered. |
| `NearFull`  | int  | `90`    | Count at or above which "Near Full" is triggered.  |
| `Full`      | int  | `100`   | Maximum capacity for "Full" status.                |

### Denomination Specifics (`[Inventory.<Currency>.Denominations.<ID>]`)
Detailed settings for a specific denomination (e.g., $100 Bill, 100 Yen Coin).

| Key             | Type   | Description                                       |
| :-------------- | :----- | :------------------------------------------------ |
| `DisplayName`   | string | English display name.                             |
| `DisplayNameJP` | string | Japanese display name.                            |
| `InitialCount`  | int    | Starting count when resetting state.              |
| `NearEmpty`     | int    | Override for Near Empty (set `-1` to disable).    |
| `NearFull`      | int    | Override for Near Full (set `-1` to disable).     |
| `Full`          | int    | Override for Full capacity (set `-1` to disable). |
| `IsRecyclable`  | bool   | Whether to use this for dispensing (recycling).   |
| `IsDepositable` | bool   | Whether to allow depositing this denomination.    |

## 6. Configuring via WPF App
In the WPF version, you can use the "System Settings" window to:
1. **General Settings**: Change currency, language, and UI mode.
2. **Denomination Calibration**: Edit counts and thresholds for each denomination.
3. **Reset**: Use the "Default" button to restore factory settings (default `SimulatorConfiguration`).

> [!IMPORTANT]
> A restart is required for changes to `CurrencyCode` to take effect.
