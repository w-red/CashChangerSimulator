using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Wpf.Services;

/// <summary>シミュレータの設定操作（保存、リロード、デフォルト復元等）を抽象化するサービスインターフェース。</summary>
public interface ISettingsOperationService
{
    /// <summary>現在の設定を指定されたオブジェクトの内容で保存し、システム全体に反映させます。</summary>
    /// <param name="config">保存する設定内容を含む <see cref="SimulatorConfiguration"/> オブジェクト。</param>
    void SaveConfig(SimulatorConfiguration config);

    /// <summary>指定された通貨コードに基づき、設定をデフォルトの状態にリセットした設定オブジェクトを取得します。</summary>
    /// <param name="currencyCode">リセット対象の通貨コード。</param>
    /// <returns>デフォルト値が設定された <see cref="SimulatorConfiguration"/>。</returns>
    SimulatorConfiguration GetDefaultConfig(string currencyCode);
}
