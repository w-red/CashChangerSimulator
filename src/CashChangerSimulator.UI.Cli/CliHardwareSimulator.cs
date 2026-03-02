using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.UI.Cli;

/// <summary>
/// CLI環境専用の HardwareSimulator 派生クラス。
/// </summary>
public class CliHardwareSimulator : HardwareSimulator
{
    public CliHardwareSimulator() : base(null)
    {
    }
}
