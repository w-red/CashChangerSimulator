using CashChangerSimulator.Core.Managers;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Core.Tests.Managers;

/// <summary>Test class for providing HardwareStatusManagerTest functionality.</summary>
public class HardwareStatusManagerTest
{
    /// <summary>Tests the behavior of ResetErrorShouldClearAllErrorFlags to ensure proper functionality.</summary>
    [Fact]
    public void ResetErrorShouldClearAllErrorFlags()
    {
        // Arrange
        using var manager = new HardwareStatusManager();
        manager.SetJammed(true);
        manager.SetOverlapped(true);
        
        manager.IsJammed.Value.ShouldBeTrue();
        manager.IsOverlapped.Value.ShouldBeTrue();

        // Act
        manager.ResetError();

        // Assert
        manager.IsJammed.Value.ShouldBeFalse();
        manager.IsOverlapped.Value.ShouldBeFalse();
    }
}