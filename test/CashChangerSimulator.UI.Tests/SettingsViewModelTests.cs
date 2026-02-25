namespace CashChangerSimulator.UI.Tests;

using R3;

using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.Core.Services;
using Shouldly;
using Xunit;
using System.Windows.Input;

/// <summary>SettingsViewModel のバリデーションとコマンドの動作を検証するテスト。</summary>
public class SettingsViewModelTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly Inventory _inventory;

    public SettingsViewModelTests()
    {
        _inventory = new Inventory();
        _configProvider = new ConfigurationProvider();
        _configProvider.Config.CurrencyCode = "JPY"; // 明示的に初期化
        _metadataProvider = new CurrencyMetadataProvider(_configProvider);
        _monitorsProvider = new MonitorsProvider(_inventory, _configProvider, _metadataProvider);
    }

    /// <summary>初期値が正しくロードされることを検証します。</summary>
    /// <remarks>
    /// 設定プロバイダーの値を変更し、ViewModel 生成時にその値が反映されていることを確認します。
    /// </remarks>
    [Fact]
    public void LoadFromConfigShouldSetInitialValues()
    {
        // Arrange
        _configProvider.Config.CurrencyCode = "USD";
        _configProvider.Config.Thresholds.NearEmpty = 10;
        _configProvider.Config.Thresholds.NearFull = 90;
        _configProvider.Config.Thresholds.Full = 100;

        // Act
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);

        // Assert
        vm.CurrencyCode.Value.ShouldBe("USD");
        vm.NearEmpty.Value.ShouldBe(10);
        vm.NearFull.Value.ShouldBe(90);
        vm.Full.Value.ShouldBe(100);
    }

    /// <summary>有効な入力値の場合にバリデーションエラーが発生しないことを検証する。</summary>
    [Fact]
    public void ValidValuesShouldNotHaveErrors()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);

        // Act
        vm.NearEmpty.Value = 5;
        vm.NearFull.Value = 10;
        vm.Full.Value = 15;

        // Assert
        vm.NearEmpty.HasErrors.ShouldBeFalse();
        vm.NearFull.HasErrors.ShouldBeFalse();
        vm.Full.HasErrors.ShouldBeFalse();
        ((ICommand)vm.SaveCommand).CanExecute(null).ShouldBeTrue();
    }

    /// <summary>NearEmpty が 0 以下の場合にバリデーションエラーが発生することを検証する。</summary>
    [Fact]
    public void NearEmptyShouldBePositive()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);

        // Act
        vm.NearEmpty.Value = 0;

        // Assert
        vm.NearEmpty.HasErrors.ShouldBeTrue();
        ((ICommand)vm.SaveCommand).CanExecute(null).ShouldBeFalse();
    }

    /// <summary>NearFull が NearEmpty 以下の時にバリデーションエラーが発生することを検証する。</summary>
    [Fact]
    public void NearFullShouldBeGreaterThanNearEmpty()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);

        // Act
        vm.NearEmpty.Value = 5;
        vm.NearFull.Value = 5; // Invalid (must be > NearEmpty)

        // Assert
        vm.NearFull.HasErrors.ShouldBeTrue();
        ((ICommand)vm.SaveCommand).CanExecute(null).ShouldBeFalse();
    }

    /// <summary>Full が NearFull 以下の時にバリデーションエラーが発生することを検証する。</summary>
    [Fact]
    public void FullShouldBeGreaterThanNearFull()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);

        // Act
        vm.NearFull.Value = 10;
        vm.Full.Value = 10; // Invalid (must be > NearFull)

        // Assert
        vm.Full.HasErrors.ShouldBeTrue();
        ((ICommand)vm.SaveCommand).CanExecute(null).ShouldBeFalse();
    }

    /// <summary>ResetToDefaultCommand が値をデフォルトに戻すことを検証する。</summary>
    [Fact]
    public void ResetToDefaultShouldRestoreValues()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);
        vm.NearEmpty.Value = 50;
        vm.NearFull.Value = 100;
        vm.Full.Value = 150;

        // Act
        vm.ResetToDefaultCommand.Execute(Unit.Default);

        // Assert: デフォルト値に戻っていること (デフォルトは NearEmpty=5, NearFull=90, Full=100)
        vm.NearEmpty.Value.ShouldBe(5);
        vm.NearFull.Value.ShouldBe(90);
        vm.Full.Value.ShouldBe(100);
    }

    /// <summary>SaveCommand が実行された時に、プロバイダーと設定が更新されることを検証します。</summary>
    /// <remarks>
    /// ViewModel で値を変更して保存を実行し、元の設定オブジェクトと再通知イベントが発生することを確認します。
    /// </remarks>
    [Fact]
    public void SaveCommandShouldUpdateConfigAndMonitors()
    {
        // Arrange
        using var vm = new SettingsViewModel(_configProvider, _monitorsProvider, _metadataProvider);
        bool reloadFired = false;
        using var _ = _configProvider.Reloaded.Subscribe(_ => reloadFired = true);

        // Act
        vm.NearEmpty.Value = 3;
        vm.NearFull.Value = 6;
        vm.Full.Value = 9;

        // Debug validation errors if any
        vm.NearEmpty.HasErrors.ShouldBeFalse();
        vm.NearFull.HasErrors.ShouldBeFalse();
        vm.Full.HasErrors.ShouldBeFalse();
        
        // Ensure CanExecute is updated
        ((ICommand)vm.SaveCommand).CanExecute(null).ShouldBeTrue();

        vm.SaveCommand.Execute(Unit.Default);

        // Assert
        vm.SaveSucceeded.Value.ShouldBeTrue();
        _configProvider.Config.Thresholds.NearEmpty.ShouldBe(3);
        _configProvider.Config.Thresholds.NearFull.ShouldBe(6);
        _configProvider.Config.Thresholds.Full.ShouldBe(9);
        reloadFired.ShouldBeTrue();
    }
}
