using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.ViewModels;

public class AdvancedSimulationViewModelTest : IClassFixture<PosTransactionViewModelFixture>, IDisposable
{
    private readonly PosTransactionViewModelFixture _fixture;
    private readonly Mock<IScriptExecutionService> _mockScriptExecutionService = new();
    private readonly AdvancedSimulationViewModel _viewModel;

    public AdvancedSimulationViewModelTest(PosTransactionViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        _viewModel = fixture.CreateAdvancedSimulationViewModel();
    }
    /// <summary>IsRealTimeDataEnabledを切り替えた際に、対象となるデバイスへ設定が反映されることを検証します。</summary>
    [Fact]
    public void IsRealTimeDataEnabledToggleShouldUpdateTargetDevice()
    {
        // Arrange
        _fixture.CashChanger.RealTimeDataEnabled = false;
 
        // Act
        _viewModel.IsRealTimeDataEnabled.Value = true;
 
        // Assert
        _fixture.CashChanger.RealTimeDataEnabled.ShouldBeTrue("Toggling the view model property should update the underlying cash changer property.");
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
        
        // Mock を使った VM を作成
        var vm = _fixture.CreateAdvancedSimulationViewModel(_mockScriptExecutionService);
        vm.ScriptInput.Value = testScript;
 
        // Setup mock to verify execution
        _mockScriptExecutionService
            .Setup(s => s.ExecuteScriptAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask)
            .Verifiable();
 
        // Act
        vm.ExecuteScriptCommand.Execute(Unit.Default);
 
        // Assert
        _mockScriptExecutionService.Verify(s => s.ExecuteScriptAsync(testScript), Times.Once, "The ExecuteScriptCommand should invoke ExecuteScriptAsync with the content of ScriptInput.");
        vm.Dispose();
    }

    public void Dispose()
    {
        _viewModel.Dispose();
        GC.SuppressFinalize(this);
    }
}
