using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

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

        // Wait for connection (relaxed check since we have auto-open now)
        var closeBtn = UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton")), TimeSpan.FromSeconds(20));
        if (closeBtn == null)
        {
            Console.WriteLine("[WARNING] DeviceCloseButton not found, attempting to proceed anyway (auto-open might be in progress).");
        }

        // Wait for inventory tile (increased timeout and visibility check)
        var inventoryTile = UiTestRetry.Find(() =>
        {
            var tiles = window.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile"));
            var target = tiles.FirstOrDefault()?.AsButton();
            return (target != null && target.IsEnabled && !target.IsOffscreen) ? target : null;
        }, TimeSpan.FromSeconds(25)) as Button;

        inventoryTile.ShouldNotBeNull("InventoryTile not found or not ready after 25s.");
        inventoryTile.SmartClick();

        // Wait for dialog using marker
        _dialog = UiTestRetry.Find(() => 
        {
            var marker = window.FindAllDescendants().FirstOrDefault(cf => cf.Properties.AutomationId.ValueOrDefault == "DenominationDetailDialogMark");
            if (marker != null) return marker.Parent; // Root border of the view

            return window.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogViewContent")) ??
                   window.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
        }, TimeSpan.FromSeconds(20));

        if (_dialog == null)
        {
            UiTestRetry.DumpAutomationTree(window, "DenominationDetailDialog_Fail");
            throw new Exception("DenominationDetailDialogView not found.");
        }

        return _dialog;
    }

    /// <summary>金種詳細ダイアログが表示されることを検証します。</summary>
    [Fact(Timeout = 60000)]
    public void DenominationDetailDialogShouldBeVisible()
    {
        var dialog = EnsureDialogOpen();
        dialog.ShouldNotBeNull();
    }

    /// <summary>金種詳細ダイアログに枚数表示フィールドが含まれていることを検証します。</summary>
    [Fact(Timeout = 60000)]
    public void DenominationDetailDialogShouldContainCountFields()
    {
        var dialog = EnsureDialogOpen();
        var textBlocks = dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        textBlocks.Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    /// <summary>閉じるボタンをクリックした際、金種詳細ダイアログが正しく閉じられることを検証します。</summary>
    [Fact(Timeout = 60000)]
    public void DenominationDetailDialogCloseButtonShouldDismissDialog()
    {
        var dialog = EnsureDialogOpen();
        var closeButton = dialog.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"))?.AsButton();
        closeButton.ShouldNotBeNull();
        closeButton.SmartClick();

        var disappeared = Retry.WhileTrue(() =>
        {
            var remaining = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogMark"));
            return remaining != null && !remaining.IsOffscreen;
        }, TimeSpan.FromSeconds(10)).Success;
        
        disappeared.ShouldBeTrue("Dialog should be dismissed");
        _dialog = null;
    }
}
