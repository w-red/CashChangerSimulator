using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf;

public class OverallStatusAggregatorProvider
{
    public OverallStatusAggregator Aggregator { get; }

    public OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
    {
        Aggregator = new OverallStatusAggregator(monitorsProvider.Monitors);
    }
}
