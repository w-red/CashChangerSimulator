using System.Collections.Generic;
using System.Linq;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf;

public class MonitorsProvider
{
    public IReadOnlyList<CashStatusMonitor> Monitors { get; }

    // DIで解決できない引数はConfigurationProviderから取得する
    // InventoryはDIされる
    public MonitorsProvider(Inventory inventory, ConfigurationProvider configProvider)
    {
        var config = configProvider.Config;
        var denominations = new[] { 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1 };

        Monitors = denominations.Select(d => new CashStatusMonitor(
            inventory, 
            d, 
            config.Thresholds.NearEmpty, 
            config.Thresholds.NearFull, 
            config.Thresholds.Full)).ToList();
    }
}
