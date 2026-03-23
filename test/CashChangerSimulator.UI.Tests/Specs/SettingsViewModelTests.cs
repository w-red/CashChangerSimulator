using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using Shouldly;
using System.Windows.Input;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>SettingsViewModel のバリデーションとコマンドの動作を検証するテスト。</summary>
public class SettingsViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public SettingsViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
    }

    private SettingsViewModel CreateViewModel()
    {
        return _fixture.CreateSettingsViewModel();
    }

    /// <summary>初期値が正しくロードされることを検証します。</summary>
    [Fact]
    public void LoadFromConfigShouldSetInitialValues()
    {
        // Arrange
        var config = _fixture.ConfigProvider.Config;
        config.System.CurrencyCode = "USD";
        config.Thresholds.NearEmpty = 10;
        config.Thresholds.NearFull = 90;
        config.Thresholds.Full = 100;
        _fixture.ConfigProvider.Update(config);

        // Act
        using var vm = CreateViewModel();

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
        using var vm = CreateViewModel();

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
        using var vm = CreateViewModel();

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
        using var vm = CreateViewModel();

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
        using var vm = CreateViewModel();

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
        using var vm = CreateViewModel();
        vm.NearEmpty.Value = 50;
        vm.NearFull.Value = 100;
        vm.Full.Value = 150;

        // Act
        vm.ResetToDefaultCommand.Execute(Unit.Default);

        // Assert: デフォルト値に戻っていること (NearEmpty=5, NearFull=90, Full=100)
        vm.NearEmpty.Value.ShouldBe(5);
        vm.NearFull.Value.ShouldBe(90);
        vm.Full.Value.ShouldBe(100);
    }

    /// <summary>SaveCommand が実行された時に、プロバイダーと設定が更新されることを検証します。</summary>
    [Fact]
    public void SaveCommandShouldUpdateConfigAndMonitors()
    {
        // Arrange
        using var vm = CreateViewModel();
        bool reloadFired = false;
        using var _ = _fixture.ConfigProvider.Reloaded.Subscribe(_ => reloadFired = true);

        // Act
        vm.NearEmpty.Value = 3;
        vm.NearFull.Value = 6;
        vm.Full.Value = 9;
        
        vm.SaveCommand.Execute(Unit.Default);

        // Assert
        vm.SaveSucceeded.Value.ShouldBeTrue();
        _fixture.ConfigProvider.Config.Thresholds.NearEmpty.ShouldBe(3);
        _fixture.ConfigProvider.Config.Thresholds.NearFull.ShouldBe(6);
        _fixture.ConfigProvider.Config.Thresholds.Full.ShouldBe(9);
        reloadFired.ShouldBeTrue();
    }
}
