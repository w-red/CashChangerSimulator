using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>デバイスの初回起動状態（Cold Start）に関連する動作を検証するテストクラス。</summary>
public class ColdStartTest
{
    private readonly Inventory Inventory;
    private readonly HardwareStatusManager HardwareStatusManager;
    private readonly ConfigurationProvider _configProvider;

    public ColdStartTest()
    {
        Inventory = new Inventory();
        HardwareStatusManager = new HardwareStatusManager();
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
        var cashChanger = new InternalSimulatorCashChanger(
            _configProvider,
            Inventory,
            null!,
            null!,
            new DepositController(Inventory, HardwareStatusManager),
            null!,
            null!,
            HardwareStatusManager);

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

        var depositController = new DepositController(Inventory, HardwareStatusManager);
        var cashChanger = new InternalSimulatorCashChanger(
            _configProvider,
            Inventory,
            null!,
            null!,
            depositController,
            null!,
            null!,
            HardwareStatusManager);
        cashChanger.SkipStateVerification = false;

        // Act & Assert
        cashChanger.State.ShouldBe(ControlState.Closed);

        // This should fail (throw exception) when Cold Start is properly implemented
        Should.Throw<PosControlException>(() => cashChanger.BeginDeposit()).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.DispenseChange(1000)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.ReadCashCounts()).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => cashChanger.AdjustCashCounts([])).ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Closed 状態では Open 以外のライフサイクル操作が制限されることを検証する。</summary>
    [Fact]
    public void LifecycleOperationsShouldFailWhenClosed()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
        _configProvider.Update(config);

        var cashChanger = new InternalSimulatorCashChanger(
            _configProvider,
            Inventory,
            null!,
            null!,
            new DepositController(Inventory, HardwareStatusManager),
            null!,
            null!,
            HardwareStatusManager);

        // Act & Assert
        cashChanger.State.ShouldBe(ControlState.Closed);

        // Claim should fail if not open
        Should.Throw<PosControlException>(() => cashChanger.Claim(1000)).ErrorCode.ShouldBe(ErrorCode.Closed);

        // Close should NOT fail even if already closed (Idempotent)
        Should.NotThrow(() => cashChanger.Close());
    }

    /// <summary>Open された後は操作が可能になることを検証する。</summary>
    [Fact]
    public void OperationShouldSucceedAfterOpen()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.Simulation.HotStart = false;
        _configProvider.Update(config);

        var cashChanger = new InternalSimulatorCashChanger(
            _configProvider,
            Inventory,
            null!,
            null!,
            new DepositController(Inventory, HardwareStatusManager),
            null!,
            null!,
            HardwareStatusManager);

        // Act
        cashChanger.SkipStateVerification = false;
        cashChanger.Open();
        cashChanger.Claim(1000);
 
        // Assert
        cashChanger.State.ShouldNotBe(ControlState.Closed);
 
        // These should not throw even if not claimed because SkipStateVerification = true
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

        var cashChanger = new InternalSimulatorCashChanger(
            _configProvider,
            Inventory,
            null!,
            null!,
            new DepositController(Inventory, HardwareStatusManager),
            null!,
            null!,
            HardwareStatusManager);

        cashChanger.SkipStateVerification = true;
        cashChanger.SkipStateVerification = false;
        cashChanger.Open();

        // Act & Assert
        // Setting enabled when opened but NOT claimed should fail (ErrorCode.Illegal)
        Should.Throw<PosControlException>(() => cashChanger.DeviceEnabled = true).ErrorCode.ShouldBe(ErrorCode.Illegal);

        // Claiming should then allow enabling
        cashChanger.Claim(0);
        Should.NotThrow(() => cashChanger.DeviceEnabled = true);
    }
}
