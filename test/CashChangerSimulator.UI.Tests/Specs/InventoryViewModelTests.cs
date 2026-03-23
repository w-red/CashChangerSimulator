using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Shouldly;
using Moq;
using Xunit;
using R3;
using Microsoft.PointOfService;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>InventoryViewModel の動作を検証するテスト集。</summary>
public class InventoryViewModelTests : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public InventoryViewModelTests(UIViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Initialize();
        
        // デフォルトの在庫を設定（TestConstants を使用）
        _fixture.SetConfigInitialCounts(
            ("C100", TestConstants.ConfigCount100),
            ("B1000", TestConstants.ConfigCount1000)
        );
        
        _fixture.SetInventory(
            (TestConstants.Key100, TestConstants.StartCount100),
            (TestConstants.Key1000, TestConstants.StartCount1000)
        );
    }

    private InventoryViewModel CreateViewModel()
    {
        return _fixture.CreateInventoryViewModel();
    }

    /// <summary>初期状態がプロバイダーから提供された金種を持っていることを検証します。</summary>
    [Fact]
    public void InitialStateShouldHaveDenominationsFromProvider()
    {
        // Assemble
        var expectedKeys = _fixture.MetadataProvider.SupportedDenominations.ToList();
        
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.Denominations.Count().ShouldBe(expectedKeys.Count);
        vm.Denominations.Select(d => d.Key).ShouldBe(expectedKeys, ignoreOrder: true);
    }

    /// <summary>一括回収コマンドが在庫を 0 にすることを検証します。</summary>
    [Fact]
    public void CollectAllCommandShouldSetInventoryToZero()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.CollectAllCommand.Execute(Unit.Default);

        // Assert
        _fixture.Inventory.GetCount(TestConstants.Key100).ShouldBe(0);
        _fixture.Inventory.GetCount(TestConstants.Key1000).ShouldBe(0);
    }

    /// <summary>一括補充コマンドが在庫を初期設定値にすることを検証します。</summary>
    [Fact]
    public void ReplenishAllCommandShouldSetInventoryToInitialCount()
    {
        // Arrange
        var vm = CreateViewModel();
        _fixture.Inventory.SetCount(TestConstants.Key100, 0);

        // Act
        vm.ReplenishAllCommand.Execute(Unit.Default);

        // Assert
        _fixture.Inventory.GetCount(TestConstants.Key100).ShouldBe(TestConstants.ConfigCount100);
    }

    /// <summary>オープンコマンドが履歴にエントリを追加することを検証します。</summary>
    [Fact]
    public void OpenCommandShouldAddOpenEntryToHistory()
    {
        // Arrange
        var vm = CreateViewModel();
        _fixture.CashChanger.Close(); 
        vm.RecentTransactions.Clear();

        // Act
        vm.OpenCommand.Execute(Unit.Default);

        // Assert
        // スキップ検証モードでは、OpenCommand は Open, Claim, EnableDevice を実行するため
        // Open と Claim の 2 つのエントリが発生します。
        vm.RecentTransactions.Count.ShouldBe(2);
        vm.RecentTransactions.Any(t => t.Type == TransactionType.Open).ShouldBeTrue();
        vm.RecentTransactions.Any(t => t.Type == TransactionType.Claim).ShouldBeTrue();
    }

    /// <summary>入金が履歴に反映されることを検証します。</summary>
    [Fact]
    public void DepositShouldAddEntryToHistory()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.RecentTransactions.Clear();

        // Act
        _fixture.Manager.Deposit(new Dictionary<DenominationKey, int> { [TestConstants.Key100] = 1 });

        // Assert
        vm.RecentTransactions.Count.ShouldBe(1);
        vm.RecentTransactions[0].Type.ShouldBe(TransactionType.Deposit);
        vm.RecentTransactions[0].Amount.ShouldBe(100);
    }

    /// <summary>在庫の変更が各金種の ViewModel に反映されることを検証します。</summary>
    [Fact]
    public void InventoryChangeShouldUpdateTotalAmountsPerDenomination()
    {
        // Assemble
        var vm = CreateViewModel();
        var denVm = vm.Denominations.First(d => d.Key.Equals(TestConstants.Key100));
        
        // Act
        _fixture.Inventory.SetCount(TestConstants.Key100, 5);

        // Assert
        denVm.Count.Value.ShouldBe(5);
    }

    /// <summary>エラーリセットコマンドがハードウェア状態をクリアすることを検証します。</summary>
    [Fact]
    public void ResetErrorCommandShouldCallStatusResetError()
    {
        // Assemble
        var vm = CreateViewModel();
        _fixture.Hardware.SetJammed(true);

        // Act
        vm.ResetErrorCommand.Execute(Unit.Default);

        // Assert
        _fixture.Hardware.IsJammed.Value.ShouldBeFalse();
    }

    /// <summary>取引履歴が 50 件を超えた場合に古い履歴が削除されることを検証します。</summary>
    [Fact]
    public void RecentTransactionsShouldLimitTo50Entries()
    {
        // Assemble
        var vm = CreateViewModel();
        vm.RecentTransactions.Clear();
        
        // Act
        for (int i = 0; i < 60; i++)
        {
            var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, i * 100, new Dictionary<DenominationKey, int>());
            _fixture.History.Add(entry);
        }

        // Assert
        vm.RecentTransactions.Count.ShouldBe(50);
        vm.RecentTransactions.First().Amount.ShouldBe(5900); // 最新が先頭
    }

    /// <summary>グリッドの幅割合が正しく計算されることを検証します。</summary>
    [Fact]
    public void GridRatiosShouldReflectDenominationCounts()
    {
        // Assemble & Act
        var vm = CreateViewModel();

        // Assert
        vm.BillGridWidth.Value.Value.ShouldBeGreaterThan(0);
        vm.CoinGridWidth.Value.Value.ShouldBeGreaterThan(0);
    }

    /// <summary>デバイスの各状態プロパティがハードウェアの状態変化に追従することを検証します。</summary>
    [Fact]
    public void PropertyChangesShouldReflectHardwareStatus()
    {
        // Assemble
        var vm = CreateViewModel();

        // Act & Assert (Overlap)
        _fixture.Hardware.SetOverlapped(true);
        vm.IsOverlapped.Value.ShouldBeTrue();

        // Act & Assert (DeviceError)
        _fixture.Hardware.SetDeviceError(1);
        vm.IsDeviceError.Value.ShouldBeTrue();
        vm.CurrentErrorCode.Value.ShouldBe(1);

        // Act & Assert (Connected)
        _fixture.Hardware.SetConnected(false);
        vm.IsConnected.CurrentValue.ShouldBeFalse();
    }

    /// <summary>モニタの変更が通知された際、金種リストが再初期化されることを検証します。</summary>
    [Fact]
    public void MonitorsChangedShouldReinitializeDenominations()
    {
        // Assemble
        var vm = CreateViewModel();
        var initialCount = vm.Denominations.Count();

        // Act
        _fixture.Monitors.TriggerChanged();

        // Assert
        vm.Denominations.Count().ShouldBe(initialCount);
    }

    /// <summary>各種例外発生時のエラーハンドリングを検証します。</summary>
    [Theory]
    [InlineData(nameof(UIViewModelFixture.CashChanger.SimulateOpenException))]
    [InlineData(nameof(UIViewModelFixture.CashChanger.SimulateCloseException))]
    public void DeviceCommandsShouldHandleExceptions(string exceptionFlag)
    {
        // Arrange
        var vm = CreateViewModel();
        if (exceptionFlag == nameof(_fixture.CashChanger.SimulateOpenException))
            _fixture.CashChanger.SimulateOpenException = true;
        else
            _fixture.CashChanger.SimulateCloseException = true;

        // Act
        if (exceptionFlag == nameof(_fixture.CashChanger.SimulateOpenException))
            vm.OpenCommand.Execute(Unit.Default);
        else
            vm.CloseCommand.Execute(Unit.Default);

        // Assert
        vm.IsDeviceError.Value.ShouldBeTrue();
        _fixture.NotifyServiceMock.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>ExportHistoryCommand が保存ダイアログを表示し、エクスポートを実行することを検証します。</summary>
    [Fact]
    public void ExportHistoryCommandShouldShowDialogAndExport()
    {
        // Assemble
        var vm = CreateViewModel();
        var path = "export.csv";
        _fixture.ViewServiceMock.Setup(v => v.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(path);
        
        // Act
        vm.ExportHistoryCommand.Execute(Unit.Default);

        // Assert
        _fixture.ViewServiceMock.Verify(v => v.ShowSaveFileDialog(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _fixture.ExportServiceMock.Verify(s => s.Export(It.IsAny<IEnumerable<TransactionEntry>>()), Times.Once);
        _fixture.NotifyServiceMock.Verify(n => n.ShowInfo(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>Dispose 呼び出しが例外を発生させないことを検証します。</summary>
    [Fact]
    public void DisposeShouldNotThrow()
    {
        // Assemble
        var vm = CreateViewModel();

        // Act & Assert
        Should.NotThrow(() => vm.Dispose());
    }
}
