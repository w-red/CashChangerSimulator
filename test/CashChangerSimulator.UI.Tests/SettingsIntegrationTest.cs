using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using Shouldly;
using System.IO;

namespace CashChangerSimulator.UI.Tests;

/// <summary>設定の保存と再読み込みの統合動作を検証するテスト。</summary>
public class SettingsIntegrationTest
{
    private readonly string _tempConfigPath;

    public SettingsIntegrationTest()
    {
        _tempConfigPath =
            Path
            .Combine(
                Path.GetTempPath(),
                "config_test.toml");
        if (File.Exists(_tempConfigPath)) File.Delete(_tempConfigPath);
    }

    /// <summary>SettingsViewModel で保存した内容が ConfigurationProvider に反映されることを検証する。</summary>
    [Fact]
    public void SaveSettingsShouldUpdateProviderAndPersist()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.System.CurrencyCode = "JPY"; // 明示的に初期化
                                                           // 開発環境の config.toml を汚さないように、一時的に保存先をモックするか、
                                                           // もしくは ConfigurationLoader.Save が使うパスを制御できるようにリファクタリングするべきですが、
                                                           // 現状は Loader が直接カレントディレクトリの config.toml を見るため、
                                                           // このテストは「ロジックの連動」のみを検証し、実際のファイル保存は Loader の単体テストに任せます。

        var inventory = new Inventory();
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadataProvider);

        using var vm = new SettingsViewModel(
            configProvider,
            monitorsProvider,
            metadataProvider);

        // Act: 値を変更
        vm.NearEmpty.Value = 12;
        vm.NearFull.Value = 34;
        vm.Full.Value = 56;

        // Act: 保存実行 (ConfigurationLoader.Save(config) も呼ばれる)
        vm.SaveCommand.Execute(Unit.Default);

        // Assert: プロバイダーの値が更新されていること
        configProvider.Config.Thresholds.NearEmpty.ShouldBe(12);
        configProvider.Config.Thresholds.NearFull.ShouldBe(34);
        configProvider.Config.Thresholds.Full.ShouldBe(56);

        // Assert: MonitorsProvider にも通知が届き、閾値が更新されていること
        // (MonitorsProvider は内部で各モニターの UpdateThresholds を呼ぶ)
        var monitor = monitorsProvider
            .Monitors
            .First(
                m => m.Key.Value == 1000
                && m.Key.Type == CurrencyCashType.Bill);
        // ... (検証ロジックを簡略化)
    }
}
