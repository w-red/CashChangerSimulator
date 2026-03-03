using CashChangerSimulator.Core.Managers;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Core.Tests.Managers;

/// <summary>HardwareStatusManager のエラー状態管理機能を検証するテストクラス。</summary>
public class HardwareStatusManagerTest
{
    /// <summary>エラーリセット操作により、すべてのエラーフラグがクリアされることを検証する。</summary>
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