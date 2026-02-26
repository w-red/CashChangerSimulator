using CashChangerSimulator.Core.Managers;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Core.Tests.Managers;

public class HardwareStatusManagerTest
{
    [Fact]
    public void ResetError_ShouldClearAllErrorFlags()
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
