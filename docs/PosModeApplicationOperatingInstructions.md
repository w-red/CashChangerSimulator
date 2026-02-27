# POS Mode Operating Instructions

POS Mode is optimized for integrating with external POS software and testing error-handling scenarios.

## 1. Simulating POS Lifecycle

Use the "Advanced Simulation" or "POS State" sections in the UI to emulate device lifecycle events:

- **Connected/Disconnected**: Trigger device arrival (Inserted) and departure (Removed) events.
- **Claim/Release**: Emulate the POS application taking or releasing ownership of the device.

## 2. Triggering Error Scenarios

Use the following controls to simulate hardware failures and logic errors:

### Mechanical Jam
- Toggle "Simulate Jam" to ON.
- Any subsequent deposit or dispense calls will return the standard UPOS `ErrorCode.Failure`.

### Cash Shortage (OverDispense)
- Set specific denominations to 0 in the inventory, then attempt an operation that requires them.
- This will trigger a `UposCashChangerErrorCodeExtended.OverDispense` (201) error.

## 3. Scripted Automation

Load JSON script files to execute sequential scenario tests (e.g., Deposit -> Confirm -> Dispense -> Jam).

1. Select the `Scripts` tab.
2. Load a JSON scenario file.
3. Click "Run Script". Progress and results will be shown in the log window.

## 4. Verifying Real-Time Notifications

- Enable the `RealTimeDataEnabled` property to test behavior where `DataEvent` is fired for every single coin/bill inserted. 
- Useful for validating POS UIs that update the total amount incrementally.

## 5. DirectIO Special Operations

Communicate with the simulator using the `DirectIO` method for vendor-specific commands (e.g., bulk inventory adjustment via string).
See the [UPOS Compliance Mapping](UposComplianceMapping.md) for a list of supported command codes.

---
*For the Japanese version, see [PosModeApplicationOperatingInstructions_JP.md](PosModeApplicationOperatingInstructions_JP.md).*
