using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.ViewModels;

/// <summary>
/// AdvancedSimulationViewModel の単体テスト。
/// </summary>
public class AdvancedSimulationViewModelTest : IDisposable
{
    private readonly Mock<InternalSimulatorCashChanger> _mockCashChanger;
    private readonly InternalSimulatorCashChanger _cashChanger;
    private readonly Mock<IScriptExecutionService> _mockScriptExecutionService;
    private readonly Mock<DepositController> _mockDepositController;
    private readonly AdvancedSimulationViewModel _viewModel;

    public AdvancedSimulationViewModelTest()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.System.CurrencyCode = "JPY";
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var hardware = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitors = new MonitorsProvider(inventory, configProvider, metadataProvider);
        var aggregator = new OverallStatusAggregatorProvider(monitors);

        _mockDepositController = new Mock<DepositController>(inventory, hardware, manager, configProvider);
        _mockDepositController.Setup(c => c.Changed).Returns(Observable.Empty<Unit>());

        var dummyDispense = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);

        _mockCashChanger = new Mock<InternalSimulatorCashChanger>(
            configProvider,
            inventory,
            history,
            manager,
            _mockDepositController.Object,
            dummyDispense,
            aggregator,
            hardware);

        _mockCashChanger.SetupProperty(x => x.RealTimeDataEnabled);
        _cashChanger = _mockCashChanger.Object;
        _mockScriptExecutionService = new Mock<IScriptExecutionService>();

        _viewModel = new AdvancedSimulationViewModel(_cashChanger, _mockScriptExecutionService.Object, _mockDepositController.Object, metadataProvider);
    }

    /// <summary>IsRealTimeDataEnabledを切り替えた際に、対象となるデバイスへ設定が反映されることを検証します。</summary>
    [Fact]
    public void IsRealTimeDataEnabledToggleShouldUpdateTargetDevice()
    {
        // Arrange
        _cashChanger.RealTimeDataEnabled = false;

        // Act
        _viewModel.IsRealTimeDataEnabled.Value = true;

        // Assert
        _cashChanger.RealTimeDataEnabled.ShouldBeTrue("Toggling the view model property should update the underlying cash changer property.");
    }

    /// <summary>有効なJSON形式の文字列が入力された場合、実行コマンドが有効状態になることを検証します。</summary>
    [Fact]
    public void ScriptInputWithValidJsonShouldEnableExecuteCommand()
    {
        // Arrange
        _viewModel.ScriptInput.Value = string.Empty;
        ((System.Windows.Input.ICommand)_viewModel.ExecuteScriptCommand).CanExecute(null).ShouldBeFalse("Empty script should disable the command.");

        // Act
        _viewModel.ScriptInput.Value = "[{ \"Op\": \"BeginDeposit\" }]";

        // Assert
        ((System.Windows.Input.ICommand)_viewModel.ExecuteScriptCommand).CanExecute(null).ShouldBeTrue("Valid JSON should enable the command.");
        _viewModel.ScriptError.Value.ShouldBeNullOrEmpty("There should be no script error for valid JSON.");
    }

    /// <summary>無効なJSON形式の文字列が入力された場合、実行コマンドが無効になり、エラーメッセージが設定されることを検証します。</summary>
    [Fact]
    public void ScriptInputWithInvalidJsonShouldDisableExecuteCommandAndShowError()
    {
        // Arrange
        _viewModel.ScriptInput.Value = "[{ \"Op\": \"BeginDeposit\" }]";

        // Act
        _viewModel.ScriptInput.Value = "INVALID_JSON";

        // Assert
        ((System.Windows.Input.ICommand)_viewModel.ExecuteScriptCommand).CanExecute(null).ShouldBeFalse("Invalid JSON should disable the command.");
        _viewModel.ScriptError.Value.ShouldNotBeNullOrEmpty("A script parsing error should be displayed.");
    }

    /// <summary>実行コマンドを呼び出した際に、正しくスクリプト実行サービスにJSONデータが渡されることを検証します。</summary>
    [Fact]
    public async Task ExecuteCommandShouldInvokeScriptExecutionService()
    {
        // Arrange
        var testScript = "[{ \"Op\": \"BeginDeposit\" }]";
        _viewModel.ScriptInput.Value = testScript;

        // Setup mock to verify execution
        _mockScriptExecutionService
            .Setup(s => s.ExecuteScriptAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        _viewModel.ExecuteScriptCommand.Execute(Unit.Default);

        // Assert
        _mockScriptExecutionService.Verify(s => s.ExecuteScriptAsync(testScript), Times.Once, "The ExecuteScriptCommand should invoke ExecuteScriptAsync with the content of ScriptInput.");
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
