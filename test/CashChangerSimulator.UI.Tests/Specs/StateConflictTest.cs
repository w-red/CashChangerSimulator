using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>状態競合時の警告ダイアログ動作を検証するテストクラス。</summary>
public class StateConflictTest : IAsyncLifetime
{
    private readonly StateConflictTestFixture _fixture = new();
    private MainViewModel _mainViewModel = null!;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        _fixture.Initialize();


        var facade = new DeviceFacade(
            _fixture.Inventory,
            _fixture.Manager,
            _fixture.DepositController,
            _fixture.MockDispenseController.Object,
            _fixture.HardwareManager,
            _fixture.MockCashChanger.Object,
            _fixture.History,
            _fixture.AggregatorProvider,
            _fixture.MonitorsProvider,
            _fixture.MockNotify.Object,
            new ImmediateDispatcherService(),
            new Mock<IViewService>().Object);

        var services = new ServiceCollection();
        services.AddSingleton(facade);
        services.AddSingleton<IDeviceFacade>(facade);
        services.AddSingleton(_fixture.ConfigProvider);
        services.AddSingleton(_fixture.MetadataProvider);
        services.AddSingleton(_fixture.MockNotify.Object); // Ensure mock is used
        services.AddTestWpfUiServices();

        var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IViewModelFactory>();

        _mainViewModel = new MainViewModel(
            factory,
            facade,
            _fixture.ConfigProvider,
            _fixture.MetadataProvider,
            facade.Notify,
            provider.GetRequiredService<IScriptExecutionService>());
        
        await ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _mainViewModel?.Dispose();
        _fixture?.Dispose();
        GC.SuppressFinalize(this);
        await ValueTask.CompletedTask;
    }

    /// <summary>入金中のディスペンス試行時に警告が表示されることを検証する。</summary>
    [Fact]
    public void DispenseShouldShowWarningDuringDeposit()
    {
        // Arrange
        _fixture.DepositController.BeginDeposit();
        _mainViewModel.Deposit.IsInDepositMode.Value.ShouldBeTrue();

        // Act
        var exception = Record.Exception(() =>
        {
            _mainViewModel.Dispense.DispenseAmountInput.Value = "1000";
            _mainViewModel.Dispense.DispenseCommand.Execute(Unit.Default);
        });

        // Assert
        exception.ShouldBeNull($"ディスペンスコマンド実行中に例外が発生しました: {exception?.Message}");

        _fixture.MockNotify.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once,
            "入金中のディスペンス試行時に警告ダイアログが表示されなかった");
    }

    /// <summary>ディスペンス中の入金試行時に警告が表示されることを検証する。</summary>
    [Fact]
    public void DepositShouldShowWarningDuringDispense()
    {
        // Arrange
        _fixture.MockDispenseController.SetupGet(c => c.Status).Returns(CashDispenseStatus.Busy);
        _fixture.DispenseChanged.OnNext(Unit.Default);

        _mainViewModel.Dispense.IsBusy.Value.ShouldBeTrue();

        // Act
        var exception = Record.Exception(() =>
        {
            _mainViewModel.Deposit.BeginDepositCommand.Execute(Unit.Default);
        });

        // Assert
        exception.ShouldBeNull($"入金コマンド実行中に例外が発生しました: {exception?.Message}");

        _fixture.MockNotify.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Once,
            "ディスペンス中の入金試行時に警告ダイアログが表示されなかった");
    }

    /// <summary>複数回の状態競合試行時に、各回ごとに警告が表示されることを検証する。</summary>
    [Fact]
    public void MultipleConflictAttemptsShouldShowWarningEachTime()
    {
        // Arrange
        _fixture.DepositController.BeginDeposit();

        // Act: 複数回ディスペンスを試行
        for (int i = 0; i < 3; i++)
        {
            var exception = Record.Exception(() =>
            {
                _mainViewModel.Dispense.DispenseAmountInput.Value = "1000";
                _mainViewModel.Dispense.DispenseCommand.Execute(Unit.Default);
            });

            exception.ShouldBeNull($"ループ {i + 1} 回目でディスペンスコマンド実行中に例外が発生しました: {exception?.Message}");
        }

        // Assert: 3回警告が表示される
        _fixture.MockNotify.Verify(
            n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(3),
            "複数回の状態競合時に、各回ごとに警告が表示されなかった");
    }

    /// <summary>入金モード中に出金コマンドが非活性になることを検証する。</summary>
    [Fact]
    public void DispenseCommandShouldBeDisabledWhenInDepositMode()
    {
        // Arrange
        _fixture.DepositController.BeginDeposit();
        _mainViewModel.Deposit.IsInDepositMode.Value.ShouldBeTrue();

        // Assert
        ((System.Windows.Input.ICommand)_mainViewModel.Dispense.DispenseCommand).CanExecute(null).ShouldBeFalse("入金中に通常出金ボタンが有効になっている");
        ((System.Windows.Input.ICommand)_mainViewModel.Dispense.ShowBulkDispenseCommand).CanExecute(null).ShouldBeFalse("入金中に一括出金ボタンが有効になっている");
    }

    /// <summary>出金処理中に入金開始コマンドが非活性になることを検証する。</summary>
    [Fact]
    public void BeginDepositCommandShouldBeDisabledWhenDispenseIsBusy()
    {
        // Arrange
        _fixture.MockDispenseController.SetupGet(c => c.Status).Returns(CashDispenseStatus.Busy);
        _fixture.DispenseChanged.OnNext(Unit.Default);
        _mainViewModel.Dispense.IsBusy.Value.ShouldBeTrue();

        // Assert
        ((System.Windows.Input.ICommand)_mainViewModel.Deposit.BeginDepositCommand).CanExecute(null).ShouldBeFalse("出金中に入金開始ボタンが有効になっている");
        ((System.Windows.Input.ICommand)_mainViewModel.Deposit.QuickDepositCommand).CanExecute(null).ShouldBeFalse("出金中にクイック入金ボタンが有効になっている");
    }
}
