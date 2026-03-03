using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Tests;

/// <summary>デバイスの初回起動状態（Cold Start）に関連する動作を検証するテストクラス。</summary>
public class ColdStartTest
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ConfigurationProvider _configProvider;

    public ColdStartTest()
    {
        _inventory = new Inventory();
        _hardwareStatusManager = new HardwareStatusManager();
        _configProvider = new ConfigurationProvider();
    }

    /// <summary>Cold Start が有効な場合、デバイスが Closed 状態で初期化されることを検証する。</summary>
    [Fact]
    public void ColdStartEnabledShouldInitializeAsClosed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.ColdStart = true;
        _configProvider.Update(config);

        // Act
        var cashChanger = new SimulatorCashChanger(
            _configProvider,
            _inventory,
            null,
            null,
            new DepositController(_inventory, _hardwareStatusManager),
            null,
            null,
            _hardwareStatusManager);

        // Assert
        cashChanger.State.ShouldBe(ControlState.Closed);
    }

    /// <summary>デバイスが Closed 状態の時に各種操作が失敗し、ErrorCode.Closed が返されることを検証する。</summary>
    [Fact]
    public void OperationShouldFailWhenDeviceIsClosed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.ColdStart = true;
        _configProvider.Update(config);

        var depositController = new DepositController(_inventory, _hardwareStatusManager);
        var cashChanger = new SimulatorCashChanger(
            _configProvider,
            _inventory,
            null,
            null,
            depositController,
            null,
            null,
            _hardwareStatusManager);

        // Act & Assert
        cashChanger.State.ShouldBe(ControlState.Closed);
        
        // This should fail (throw exception) when Cold Start is properly implemented
        var ex = Should.Throw<PosControlException>(() => cashChanger.BeginDeposit());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }
}
