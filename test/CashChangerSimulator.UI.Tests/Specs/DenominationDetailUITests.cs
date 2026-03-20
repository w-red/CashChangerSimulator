using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using FlaUI.Core.Capturing;
using FlaUI.Core.Input;
using Shouldly;
using Xunit;
using CashChangerSimulator.UI.Tests.Helpers;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>
/// 金種詳細ダイアログの表示・内容・閉じる動作を検証する UI テスト。
/// </summary>
[Collection("SequentialTests")]
public class DenominationDetailUITests : IClassFixture<CashChangerTestApp>
{
    private readonly CashChangerTestApp _app;
    private AutomationElement? _dialog;

    public DenominationDetailUITests(CashChangerTestApp app)
    {
        _app = app;
    }

    private AutomationElement EnsureDialogOpen()
    {
        if (_dialog != null && !_dialog.IsOffscreen) return _dialog;

        _app.Launch(hotStart: true);
        var window = _app.MainWindow ?? throw new Exception("MainWindow is null");
        window.SetForeground();

        // 接続完了まで待機 (HotStartの場合でもUI更新を待つ)
        var closeBtn = Retry.WhileNull(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton")), TimeSpan.FromSeconds(15)).Result;
        closeBtn.ShouldNotBeNull("Device is not connected (DeviceCloseButton not found).");

        // 金種タイルの出現と有効化を待機
        var inventoryTile = Retry.WhileNull(() =>
        {
            var tiles = window.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile"));
            var target = tiles.FirstOrDefault()?.AsButton();
            return (target != null && target.IsEnabled && !target.IsOffscreen) ? target : null;
        }, TimeSpan.FromSeconds(15)).Result;

        inventoryTile.ShouldNotBeNull("InventoryTile not found or not ready.");
        
        // ダイアログを開く
        inventoryTile.SmartClick();

        // ダイアログの出現を待機
        _dialog = Retry.WhileNull(() => 
        {
            // 複数の ID で確実に見つける
            return window.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogMark"))?.Parent ??
                   window.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogViewContent")) ??
                   window.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView")) ??
                   UiTestRetry.FindWindow(_app.Application, _app.Automation, "DenominationDetailDialogView", TimeSpan.FromMilliseconds(500));
        }, TimeSpan.FromSeconds(20)).Result;

        _dialog.ShouldNotBeNull("DenominationDetailDialogView not found.");
        return _dialog;
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_ShouldBeVisible()
    {
        var dialog = EnsureDialogOpen();
        dialog.ShouldNotBeNull();
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_ShouldContainCountFields()
    {
        var dialog = EnsureDialogOpen();
        var textBlocks = dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        textBlocks.Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_CloseButton_ShouldDismissDialog()
    {
        var dialog = EnsureDialogOpen();
        var closeButton = dialog.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"))?.AsButton();
        closeButton.ShouldNotBeNull();
        closeButton.SmartClick();

        Retry.WhileTrue(() =>
        {
            var remaining = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
            return remaining != null && !remaining.IsOffscreen;
        }, TimeSpan.FromSeconds(10)).Success.ShouldBeTrue("Dialog should be dismissed");
        
        _dialog = null; // 使い終わったのでリセット
    }
}
