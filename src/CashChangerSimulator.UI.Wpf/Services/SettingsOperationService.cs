using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>シミュレータの設定操作を具現化するサービスの実装クラス。</summary>
public class SettingsOperationService(
    ConfigurationProvider configProvider,
    MonitorsProvider monitorsProvider,
    ILogger<SettingsOperationService> logger) : ISettingsOperationService
{
    /// <inheritdoc/>
    public void SaveConfig(SimulatorConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        // ファイルへの保存
        ConfigurationLoader.Save(config);

        // メモリ上の設定をリロード
        configProvider.Reload();

        // 監視マネージャーのしきい値を更新
        monitorsProvider.UpdateThresholdsFromConfig(configProvider.Config);

        logger.ZLogInformation($"Simulator configuration saved and system reloaded via SettingsOperationService.");
    }

    /// <inheritdoc/>
    public SimulatorConfiguration GetDefaultConfig(string currencyCode)
    {
        var defaultConfig = new SimulatorConfiguration
        {
            System = { CurrencyCode = currencyCode }
        };
        // デフォルトインスタンスは型定義時に既定値がセットされているため、
        // 通貨コードのみ設定して返却します。
        return defaultConfig;
    }
}
