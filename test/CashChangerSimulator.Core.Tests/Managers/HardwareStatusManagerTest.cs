using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Core.Tests.Managers;

/// <summary>HardwareStatusManager のエラー状態管理機能を検証するテストクラス。</summary>
public class HardwareStatusManagerTest
{
    /// <summary>ジャム箇所を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetJammedWithLocationShouldStoreCorrectLocation()
    {
        // Arrange
        using var manager = new HardwareStatusManager();

        // Act
        manager.SetJammed(true, JamLocation.BillCassette1);

        // Assert
        manager.IsJammed.Value.ShouldBeTrue();
        manager.JamLocation.Value.ShouldBe(JamLocation.BillCassette1);
    }

    /// <summary>ジャム解除時にジャム箇所も None になることを検証する。</summary>
    [Fact]
    public void SetJammedFalseShouldClearLocation()
    {
        // Arrange
        using var manager = new HardwareStatusManager();
        manager.SetJammed(true, JamLocation.Inlet);

        // Act
        manager.SetJammed(false);

        // Assert
        manager.IsJammed.Value.ShouldBeFalse();
        manager.JamLocation.Value.ShouldBe(JamLocation.None);
    }

    /// <summary>エラーリセット操作により、すべてのエラーフラグと箇所がクリアされることを検証する。</summary>
    [Fact]
    public void ResetErrorShouldClearAllErrorFlags()
    {
        // Arrange
        using var manager = new HardwareStatusManager();
        manager.SetJammed(true, JamLocation.Transport);
        manager.SetOverlapped(true);
        
        manager.IsJammed.Value.ShouldBeTrue();
        manager.IsOverlapped.Value.ShouldBeTrue();
        manager.JamLocation.Value.ShouldBe(JamLocation.Transport);

        // Act
        manager.ResetError();

        // Assert
        manager.IsJammed.Value.ShouldBeFalse();
        manager.IsOverlapped.Value.ShouldBeFalse();
        manager.JamLocation.Value.ShouldBe(JamLocation.None);
    }
}