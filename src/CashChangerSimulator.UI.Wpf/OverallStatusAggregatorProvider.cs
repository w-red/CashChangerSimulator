using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf;

public class OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
{
    public OverallStatusAggregator Aggregator { get; } = new OverallStatusAggregator(monitorsProvider.Monitors);
}
