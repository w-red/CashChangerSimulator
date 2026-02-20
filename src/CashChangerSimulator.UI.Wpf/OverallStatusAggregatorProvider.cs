using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
public class OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
{
    /// <summary>ステータス集計インスタンス。</summary>
    public OverallStatusAggregator Aggregator { get; } = new OverallStatusAggregator(monitorsProvider.Monitors);
}
