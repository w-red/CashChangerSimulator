using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Cli;

/// <summary>
/// CLI環境専用の HardwareSimulator 派生クラス。
/// </summary>
public class CliHardwareSimulator : HardwareSimulator
{
    public CliHardwareSimulator(ConfigurationProvider configProvider) : base(configProvider)
    {
    }
}
