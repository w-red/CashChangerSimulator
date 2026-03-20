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

        // Wait for connection
        var closeBtn = UiTestRetry.Find(() => window.FindFirstDescendant(cf => cf.ByAutomationId("DeviceCloseButton")), TimeSpan.FromSeconds(15));
        closeBtn.ShouldNotBeNull("Device is not connected.");

        // Wait for inventory tile
        var inventoryTile = UiTestRetry.Find(() =>
        {
            var tiles = window.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile"));
            var target = tiles.FirstOrDefault()?.AsButton();
            return (target != null && target.IsEnabled && !target.IsOffscreen) ? target : null;
        }, TimeSpan.FromSeconds(15)) as Button;

        inventoryTile.ShouldNotBeNull("InventoryTile not found or not ready.");
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

        var disappeared = Retry.WhileTrue(() =>
        {
            var remaining = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogMark"));
            return remaining != null && !remaining.IsOffscreen;
        }, TimeSpan.FromSeconds(10)).Success;
        
        disappeared.ShouldBeTrue("Dialog should be dismissed");
        _dialog = null;
    }
}
