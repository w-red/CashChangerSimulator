using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
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
    public void HotStartDisabledShouldInitializeAsClosed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
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
        config.Simulation.HotStart = false;
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
        Should.Throw<PosControlException>(() => cashChanger.BeginDeposit()).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.DispenseChange(1000)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.ReadCashCounts()).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.AdjustCashCounts(new CashCount[0])).ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Closed 状態では Open 以外のライフサイクル操作が制限されることを検証する。</summary>
    [Fact]
    public void LifecycleOperationsShouldFailWhenClosed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
        _configProvider.Update(config);

        var cashChanger = new SimulatorCashChanger(
            _configProvider,
            _inventory,
            null,
            null,
            new DepositController(_inventory, _hardwareStatusManager),
            null,
            null,
            _hardwareStatusManager);

        // Act & Assert
        cashChanger.State.ShouldBe(ControlState.Closed);

        // Claim should fail if not open
        Should.Throw<PosControlException>(() => cashChanger.Claim(1000)).ErrorCode.ShouldBe(ErrorCode.Closed);

        // Close should fail if already closed
        Should.Throw<PosControlException>(() => cashChanger.Close()).ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Open された後は操作が可能になることを検証する。</summary>
    [Fact]
    public void OperationShouldSucceedAfterOpen()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
        _configProvider.Update(config);

        var cashChanger = new SimulatorCashChanger(
            _configProvider,
            _inventory,
            null,
            null,
            new DepositController(_inventory, _hardwareStatusManager),
            null,
            null,
            _hardwareStatusManager);

        // Act
        cashChanger.Open();
        
        // Assert
        cashChanger.State.ShouldNotBe(ControlState.Closed);
        
        // These should not throw ErrorCode.Closed anymore (though they might throw other errors if not claimed/enabled)
        // Since SkipStateVerification defaults to false, VerifyState() is called.
        Should.NotThrow(() => cashChanger.BeginDeposit());
    }

    /// <summary>DeviceEnabled の設定が適切な状態（Opened & Claimed）を要求することを検証する。</summary>
    [Fact]
    public void DeviceEnabledShouldRequireClaimed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
        _configProvider.Update(config);

        var cashChanger = new SimulatorCashChanger(
            _configProvider,
            _inventory,
            null,
            null,
            new DepositController(_inventory, _hardwareStatusManager),
            null,
            null,
            _hardwareStatusManager);

        // Act & Assert
        // Setting enabled when closed should fail
        Should.Throw<PosControlException>(() => cashChanger.DeviceEnabled = true).ErrorCode.ShouldBe(ErrorCode.Closed);

        cashChanger.Open();
        
        // Setting enabled when opened but not claimed should fail (ErrorCode.Illegal is typical for this)
        Should.Throw<PosControlException>(() => cashChanger.DeviceEnabled = true).ErrorCode.ShouldBe(ErrorCode.Illegal);

        cashChanger.Claim(1000);
        
        // Now it should succeed
        Should.NotThrow(() => cashChanger.DeviceEnabled = true);
        cashChanger.DeviceEnabled.ShouldBeTrue();
    }
}
