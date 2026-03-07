using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Cli;

/// <summary>
/// CLI環境専用の ConfigurationProvider。
/// DI コンテナが引数ありコンストラクタを誤認するのを防ぐため、
/// 明示的に引数なしコンストラクタのみを公開する。
/// </summary>
public class CliConfigurationProvider : ConfigurationProvider
{
    public CliConfigurationProvider()
    {
    }
}
