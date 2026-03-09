# Standard Mode Operating Instructions

Standard Mode is designed for direct manual interaction with the simulated device via the GUI. It provides an intuitive interface to monitor inventory states and test cash transaction scenarios.

## 1. Screen Layout

- **Inventory Grid**: Displays current denomination counts and states (Connected, Discrepancy, NearFull, etc.) in real-time.
- **Control Panel (Right Panel)**: 
  - **Terminal Access**: Launches deposit and dispense simulation windows.
  - **Advanced Simulation**: Launches advanced tools for script execution and error injection.
  - **Device Control**: `Open` / `Close` buttons for standard lifecycle management.
- **Activity Feed**: A chronological list of device events (e.g., DataEvent, StatusUpdateEvent).

![Main Dashboard](images/main_dashboard.png)
*Fig: Simulator Main Dashboard*

## 2. Device Control & Lifecycle

1. **Open**: Initializes the simulator and makes it available for higher-level applications (external or built-in tools).
2. **Close**: Stops the device, releases all resources, and returns to the initial state.

## 3. Deposit & Dispense Simulation

### Manual Deposit (Deposit)
![Deposit Window](images/deposit_window.png)

1. Click the `Deposit` button to open the dedicated window.
2. Click denomination-specific buttons to "insert" cash.
3. Use the `Fix` button to validate (confirm) the inserted cash.
4. Click `End` button to finalize the transaction and update the inventory.

### Manual Dispense (Dispense)
![Dispense Window](images/dispense_window.png)

1. Click the `Dispense` button to open the dedicated window.
2. Enter the total amount or specify exact denomination counts to execute the dispense.

## 4. Inventory Management & Detailed View

### Understanding Inventory Tiles
- **Colors/Icons**: Indicators for `Connected` (Active), `Discrepancy` (Inventory mismatch), `NearFull`, and other states are represented via icons and background colors.
- **Visibility**: Improved font sizes and contrast ensure status is easily readable from a distance.

### Denomination Detail Dialog
Clicking an inventory tile opens the **Denomination Detail Dialog**.
- **Recycle**: Counts available for dispensing.
- **Collection**: Counts automatically collected during overflow or specific operations.
- **Reject**: Counts rejected due to dirt, damage, or external factors.

![Inventory Detail](images/inventory_detail.png)
*Fig: Denomination Detail Dialog*

## 5. Advanced Simulation
![Advanced Simulation](images/advanced_simulation.png)

This window allows testing scenarios that are difficult to reproduce via basic GUI operations:
- **Script Execution**: Execute JSON files defining complex cash transaction sequences.
- **Error Injection**: Force specific sensor errors (NearFull, Full, etc.) to verify top-level app behavior.

## 6. Configuration (config.toml)

Application settings are stored in `config.toml`. Key parameters can be modified via the "System Settings" window in the UI.

For detailed information on all available settings (Currency, Language, Thresholds, etc.), please refer to the [Configuration Guide](ConfigurationGuide.md).

### `[System]` — General Settings
- `CurrencyCode`: Active currency (default "JPY").
- `UIMode`: UI launch mode (Standard/PosTransaction).

### `[Inventory.<CODE>.Denominations.<KEY>]` — Per-Denom Settings
- `InitialCount`: Initial count at startup.
- `NearEmpty` / `NearFull` / `Full`: Thresholds for status determination.

---
*For the Japanese version, see [ApplicationOperatingInstructions_JP.md](ApplicationOperatingInstructions_JP.md).*
