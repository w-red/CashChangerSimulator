using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Tests.Fixtures;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Shouldly;
using Moq;
using Xunit;
using R3;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>InventoryViewModel の動作を検証するテストクラス。</summary>
public class InventoryViewModelTest : IClassFixture<PosTransactionViewModelFixture>
{
    private readonly PosTransactionViewModelFixture _fixture;

    /// <summary>テスト用のフィクスチャを初期化します。</summary>
    public InventoryViewModelTest(PosTransactionViewModelFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    /// <summary>初期状態がプロバイダーから提供された金種を持っていることを検証します。</summary>
    [Fact]
    public void InitialStateShouldHaveDenominationsFromProvider()
    {
        // Assemble
        var expectedKeys = _fixture.MetadataProvider.SupportedDenominations.ToList();
        
        // Act
        var vm = _fixture.CreateInventoryViewModel();

        // Assert
        vm.Denominations.Count().ShouldBe(expectedKeys.Count);
        vm.Denominations.Select(d => d.Key).ShouldBe(expectedKeys, ignoreOrder: true);
    }

    /// <summary>一括回収コマンドが在庫を 0 にすることを検証します。</summary>
    [Fact]
    public void CollectAllCommandShouldSetInventoryToZero()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        _fixture.Inventory.SetCount(denKey, 10);
        
        // Act
        vm.CollectAllCommand.Execute(Unit.Default);

        // Assert
        _fixture.Inventory.GetCount(denKey).ShouldBe(0);
    }

    /// <summary>一括補充コマンドが在庫を初期設定値にすることを検証します。</summary>
    [Fact]
    public void ReplenishAllCommandShouldSetInventoryToInitialCount()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        var setting = _fixture.ConfigProvider.Config.GetDenominationSetting(denKey);
        _fixture.Inventory.SetCount(denKey, 0);
        
        // Act
        vm.ReplenishAllCommand.Execute(Unit.Default);

        // Assert
        _fixture.Inventory.GetCount(denKey).ShouldBe(setting.InitialCount);
    }

    /// <summary>設定画面表示コマンドが実行可能な状態であることを検証します。</summary>
    [Fact]
    public void OpenSettingsCommandShouldBeExecutable()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();

        // Act & Assert
        vm.OpenSettingsCommand.CanExecute().ShouldBeTrue();
    }

    /// <summary>在庫の変更が各金種の ViewModel に反映されることを検証します。</summary>
    [Fact]
    public void InventoryChangeShouldUpdateTotalAmountsPerDenomination()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var denKey = _fixture.MetadataProvider.SupportedDenominations.First();
        var denVm = vm.Denominations.First(d => d.Key.Equals(denKey));
        
        // Act
        _fixture.Inventory.SetCount(denKey, 5);

        // Assert
        denVm.Count.Value.ShouldBe(5);
    }

    /// <summary>オープン・クローズコマンドがデバイス状態を変化させることを検証します。</summary>
    [Fact]
    public void OpenCloseCommandsShouldChangeDeviceStatus()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        _fixture.Hardware.SetConnected(false);

        // Act
        vm.OpenCommand.Execute(Unit.Default);

        // Assert
        // SkipStateVerification が true の場合、Open 時に接続状態が true になる
        _fixture.Hardware.IsConnected.CurrentValue.ShouldBeTrue();

        // Act 2
        vm.CloseCommand.Execute(Unit.Default);

        // Assert 2
        _fixture.Hardware.IsConnected.Value.ShouldBeFalse();
    }

    /// <summary>エラーリセットコマンドがハードウェア状態をクリアすることを検証します。</summary>
    [Fact]
    public void ResetErrorCommandShouldCallStatusResetError()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        _fixture.Hardware.SetJammed(true);

        // Act
        vm.ResetErrorCommand.Execute(Unit.Default);

        // Assert
        _fixture.Hardware.IsJammed.Value.ShouldBeFalse();
    }

    /// <summary>取引履歴の追加が ViewModel のリストに反映されることを検証します。</summary>
    [Fact]
    public void HistoryAddedShouldUpdateRecentTransactions()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 1000, new Dictionary<DenominationKey, int>());

        // Act
        _fixture.History.Add(entry);

        // Assert
        vm.RecentTransactions.ShouldContain(entry);
        vm.IsEmpty.Value.ShouldBeFalse();
    }

    /// <summary>金種数に応じてグリッドの幅割合が計算されることを検証します。</summary>
    [Fact]
    public void GridRatiosShouldReflectDenominationCounts()
    {
        // Assemble & Act
        var vm = _fixture.CreateInventoryViewModel();

        // Assert
        vm.BillGridWidth.Value.Value.ShouldBeGreaterThan(0);
        vm.CoinGridWidth.Value.Value.ShouldBeGreaterThan(0);
    }

    /// <summary>金種詳細ダイアログ表示コマンドが実行可能であることを検証します。</summary>
    [Fact]
    public void OpenFirstBillDenominationDetailCommandShouldExecuteWithoutError()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();

        // Act & Assert
        vm.OpenFirstBillDenominationDetailCommand.CanExecute().ShouldBeTrue();
    }

    /// <summary>デバイスオープン時の例外発生が正しくハンドリングされることを検証します。</summary>
    /// <remarks>例外発生時にデバイスエラー状態が設定され、警告メッセージが表示されることを確認します。</remarks>
    [Fact]
    public void OpenCommandShouldHandleExceptionAndSetError()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        _fixture.CashChanger.SimulateOpenException = true;

        // Act
        vm.OpenCommand.Execute(Unit.Default);

        // Assert
        vm.IsDeviceError.Value.ShouldBeTrue();
        _fixture.NotifyServiceMock.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>デバイスクローズ時の例外発生が正しくハンドリングされることを検証します。</summary>
    [Fact]
    public void CloseCommandShouldHandleExceptionAndSetError()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        _fixture.CashChanger.SimulateCloseException = true;

        // Act
        vm.CloseCommand.Execute(Unit.Default);

        // Assert
        vm.IsDeviceError.Value.ShouldBeTrue();
        _fixture.NotifyServiceMock.Verify(n => n.ShowWarning(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    /// <summary>デバイスの各状態プロパティがハードウェアの状態変化に追従することを検証します。</summary>
    [Fact]
    public void PropertyChangesShouldReflectHardwareStatus()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();

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

    /// <summary>紙幣金種のみ存在する場合のグリッド幅割合が正しく計算されることを検証します。</summary>
    [Fact]
    public void UpdateGridRatiosShouldHandleOnlyBills()
    {
        // Assemble
        var invSettings = new InventorySettings();
        
        // 全ての金種を一旦無効化
        foreach (var k in _fixture.MetadataProvider.SupportedDenominations)
        {
            invSettings.Denominations[k.ToDenominationString()] = new DenominationSettings { IsRecyclable = false, IsDepositable = false };
        }
        
        // 紙幣1つだけ有効化
        var targetKey = _fixture.MetadataProvider.SupportedDenominations.First(k => k.Type == CurrencyCashType.Bill);
        invSettings.Denominations[targetKey.ToDenominationString()].IsRecyclable = true;
        invSettings.Denominations[targetKey.ToDenominationString()].IsDepositable = true;
        
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "JPY";
        config.Inventory["JPY"] = invSettings;
        _fixture.ConfigProvider.Update(config);
        
        // Act
        var vm = _fixture.CreateInventoryViewModel();

        // Assert
        vm.BillGridWidth.Value.Value.ShouldBe(1);
        vm.CoinGridWidth.Value.Value.ShouldBe(0);
        vm.CoinGridWidth.Value.GridUnitType.ShouldBe(System.Windows.GridUnitType.Pixel);
    }

    /// <summary>硬貨金種のみ存在する場合のグリッド幅割合が正しく計算されることを検証します。</summary>
    [Fact]
    public void UpdateGridRatiosShouldHandleOnlyCoins()
    {
        // Assemble
        var invSettings = new InventorySettings();
        
        // 全ての金種を一旦無効化
        foreach (var k in _fixture.MetadataProvider.SupportedDenominations)
        {
            invSettings.Denominations[k.ToDenominationString()] = new DenominationSettings { IsRecyclable = false, IsDepositable = false };
        }
        
        // 硬貨1つだけ有効化
        var targetKey = _fixture.MetadataProvider.SupportedDenominations.First(k => k.Type == CurrencyCashType.Coin);
        invSettings.Denominations[targetKey.ToDenominationString()].IsRecyclable = true;
        invSettings.Denominations[targetKey.ToDenominationString()].IsDepositable = true;
        
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "JPY";
        config.Inventory["JPY"] = invSettings;
        _fixture.ConfigProvider.Update(config);
        
        // Act
        var vm = _fixture.CreateInventoryViewModel();

        // Assert
        vm.BillGridWidth.Value.Value.ShouldBe(0);
        vm.BillGridWidth.Value.GridUnitType.ShouldBe(System.Windows.GridUnitType.Pixel);
        vm.CoinGridWidth.Value.Value.ShouldBe(1);
    }

    /// <summary>金種構成が空の場合のグリッド幅割合がデフォルト値になることを検証します。</summary>
    [Fact]
    public void UpdateGridRatiosShouldHandleEmptyDenominations()
    {
        // Assemble
        _fixture.ConfigProvider.Config.Inventory.Clear();
        _fixture.Reset();
        
        // Act
        var vm = _fixture.CreateInventoryViewModel();

        // Assert
        vm.BillGridWidth.Value.GridUnitType.ShouldBe(System.Windows.GridUnitType.Star);
        vm.CoinGridWidth.Value.GridUnitType.ShouldBe(System.Windows.GridUnitType.Star);
    }

    /// <summary>通貨の接頭辞と接尾辞がメタデータプロバイダーの設定を反映していることを検証します。</summary>
    [Fact]
    public void CurrencyMetadataShouldBeReflected()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();

        // Act & Assert
        vm.CurrencyPrefix.CurrentValue.ShouldBe(_fixture.MetadataProvider.SymbolPrefix.CurrentValue);
        vm.CurrencySuffix.CurrentValue.ShouldBe(_fixture.MetadataProvider.SymbolSuffix.CurrentValue);
    }

    /// <summary>モニタの変更が通知された際、金種リストが再初期化されることを検証します。</summary>
    [Fact]
    public void MonitorsChangedShouldReinitializeDenominations()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var initialCount = vm.Denominations.Count();

        // Act
        _fixture.Monitors.TriggerChanged();

        // Assert
        vm.Denominations.Count().ShouldBe(initialCount);
    }

    /// <summary>取引履歴が 50 件を超えた場合に古い履歴が削除されることを検証します。</summary>
    [Fact]
    public void RecentTransactionsShouldLimitTo50Entries()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        
        // Act
        for (int i = 0; i < 60; i++)
        {
            var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, i * 100, new Dictionary<DenominationKey, int>());
            _fixture.History.Add(entry);
        }

        // Assert
        vm.RecentTransactions.Count.ShouldBe(50);
        vm.RecentTransactions.First().Amount.ShouldBe(5900); // 最新
    }

    /// <summary>金種詳細ダイアログ表示コマンドが例外なく実行されることを検証します。</summary>
    [Fact]
    public void ShowDenominationDetailCommandShouldExecuteWithoutError()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();
        var denVm = vm.Denominations.First();

        // Act & Assert
        Should.NotThrow(() => vm.ShowDenominationDetailCommand.Execute(denVm));
    }

    /// <summary>Dispose 呼び出しが例外を発生させないことを検証します。</summary>
    [Fact]
    public void DisposeShouldNotThrow()
    {
        // Assemble
        var vm = _fixture.CreateInventoryViewModel();

        // Act & Assert
        Should.NotThrow(() => vm.Dispose());
    }
}
