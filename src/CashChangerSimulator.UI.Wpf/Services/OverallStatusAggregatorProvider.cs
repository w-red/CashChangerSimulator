using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
/// <param name="monitorsProvider">ステータスモニタープロバイダー。</param>
public class OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
{
    /// <summary>ステータス集計インスタンス。</summary>
    public OverallStatusAggregator Aggregator { get; } = new OverallStatusAggregator(monitorsProvider.Monitors);
}
