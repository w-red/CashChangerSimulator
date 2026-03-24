using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>AdvancedSimulationViewModel の動作を検証するテストクラス。</summary>
public class AdvancedSimulationViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public AdvancedSimulationViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize(useRealScriptService: true);
    }

    private AdvancedSimulationViewModel CreateViewModel(Mock<IScriptExecutionService>? mockService = null)
    {
        return _fixture.CreateAdvancedSimulationViewModel(mockService);
    }

    /// <summary>IsRealTimeDataEnabledを切り替えた際に、対象となるデバイスへ設定が反映されることを検証します。</summary>
    [Fact]
    public void IsRealTimeDataEnabledToggleShouldUpdateTargetDevice()
    {
        // Arrange
        var vm = CreateViewModel();
        _fixture.CashChanger.RealTimeDataEnabled = false;
 
        // Act
        vm.IsRealTimeDataEnabled.Value = true;
 
        // Assert
        _fixture.CashChanger.RealTimeDataEnabled.ShouldBeTrue();
    }

    /// <summary>有効なJSON形式の文字列が入力された場合、実行コマンドが有効状態になることを検証します。</summary>
    [Fact]
    public void ScriptInputWithValidJsonShouldEnableExecuteCommand()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ScriptInput.Value = string.Empty;
        ((System.Windows.Input.ICommand)vm.ExecuteScriptCommand).CanExecute(null).ShouldBeFalse();

        // Act
        vm.ScriptInput.Value = "[{ \"Op\": \"BeginDeposit\" }]";

        // Assert
        ((System.Windows.Input.ICommand)vm.ExecuteScriptCommand).CanExecute(null).ShouldBeTrue();
        vm.ScriptError.Value.ShouldBeNullOrEmpty();
    }

    /// <summary>無効なJSON形式の文字列が入力された場合、実行コマンドが無効になり、エラーメッセージが設定されることを検証します。</summary>
    [Fact]
    public void ScriptInputWithInvalidJsonShouldDisableExecuteCommandAndShowError()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ScriptInput.Value = "[{ \"Op\": \"BeginDeposit\" }]";

        // Act
        vm.ScriptInput.Value = "INVALID_JSON";

        // Assert
        ((System.Windows.Input.ICommand)vm.ExecuteScriptCommand).CanExecute(null).ShouldBeFalse();
        vm.ScriptError.Value.ShouldNotBeNullOrEmpty();
    }

    /// <summary>実行コマンドを呼び出した際に、正しくスクリプト実行サービスにJSONデータが渡されることを検証します。</summary>
    [Fact]
    public void ExecuteCommandShouldInvokeScriptExecutionService()
    {
        // Arrange
        var testScript = "[{ \"Op\": \"BeginDeposit\" }]";
        var mockService = new Mock<IScriptExecutionService>();
        var vm = CreateViewModel(mockService);
        vm.ScriptInput.Value = testScript;
        
        mockService
            .Setup(s => s.ExecuteScriptAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
 
        // Act
        vm.ExecuteScriptCommand.Execute(Unit.Default);
 
        // Assert
        mockService.Verify(s => s.ExecuteScriptAsync(testScript), Times.Once);
    }

    /// <summary>実サービスを使用したスクリプト実行が動作することを検証します。</summary>
    [Fact]
    public void ExecuteScriptWithRealServiceShouldWork()
    {
        // Arrange
        var vm = CreateViewModel(); // Calls Initialize(useRealScriptService: true)
        vm.ScriptInput.Value = "[{ \"Op\": \"Open\" }]";

        // Act & Assert
        Should.NotThrow(() => vm.ExecuteScriptCommand.Execute(Unit.Default));
    }
}
