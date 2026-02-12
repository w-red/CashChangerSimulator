using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Wpf;

public class ConfigurationProvider
{
    public SimulatorConfiguration Config { get; }

    public ConfigurationProvider()
    {
        Config = ConfigurationLoader.Load();
    }
}
