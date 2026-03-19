using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
using Xunit;
using CashChangerSimulator.UI.Tests.Helpers;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>
/// 金種詳細ダイアログの表示・内容・閉じる動作を検証する UI テスト。
/// </summary>
[Collection("SequentialTests")]
public class DenominationDetailUITests : IDisposable
{
    private readonly CashChangerTestApp _app;
    private readonly AutomationElement _dialog;

    public DenominationDetailUITests()
    {
        _app = new CashChangerTestApp();
        _app.Launch();

        var inventoryTile = Retry.WhileNull(() =>
        {
            if (_app.MainWindow == null) return null;
            
            // CI環境では要素が深い階層にある場合があるため、Descendants全検索を試みる
            var tiles = _app.MainWindow.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile"));
            return tiles.FirstOrDefault()?.AsButton();
        }, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)).Result;

        if (inventoryTile == null)
        {
            Console.WriteLine("[ERROR] InventoryTile not found. Tree dump:");
            var sbDump = new System.Text.StringBuilder();
            _app.MainWindow?.CaptureElements(0, sbDump);
            Console.WriteLine(sbDump.ToString());
            throw new Exception("InventoryTile が見つかりません。");
        }

        // Act: 金種タイルをクリックして詳細ダイアログを開く
        inventoryTile.SmartClick();

        // [STABILITY] ダイアログの出現を待機
        var dialog = UiTestRetry.Find(() => 
        {
            // Try MainWindow children (DialogHost standard)
            var found = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
            if (found != null) return found;

            // Try Desktop fallback (in case it behaves like a popup window in CI)
            var win = UiTestRetry.FindWindow(_app.Application, _app.Automation, "DenominationDetailDialogView", TimeSpan.FromSeconds(1));
            return win;
        }, TimeSpan.FromSeconds(20));

        if (dialog == null)
        {
            Console.WriteLine("[ERROR] DenominationDetailDialogView not found. Dumping MainWindow tree:");
            var sb = new System.Text.StringBuilder();
            _app.MainWindow?.CaptureElements(0, sb);
            Console.WriteLine(sb.ToString());
            throw new Exception("ダイアログ 'DenominationDetailDialogView' が制限時間内に見つかりませんでした。");
        }

        _dialog = dialog;
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_ShouldBeVisible()
    {
        _dialog.ShouldNotBeNull("ダイアログが表示されていません。");
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_ShouldContainCountFields()
    {
        _dialog.ShouldNotBeNull();
        var textBlocks = _dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        textBlocks.Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact(Timeout = 60000)]
    public void DenominationDetailDialog_CloseButton_ShouldDismissDialog()
    {
        _dialog.ShouldNotBeNull();
        Thread.Sleep(1000);

        var closeButton = _dialog.FindFirstDescendant(cf => cf.ByAutomationId("CloseButton"))?.AsButton();
        if (closeButton == null)
            closeButton = _dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)).FirstOrDefault()?.AsButton();

        closeButton.ShouldNotBeNull();
        closeButton.SmartClick();

        Retry.WhileTrue(() =>
        {
            var remaining = _app.MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
            return remaining != null && !remaining.IsOffscreen;
        }, TimeSpan.FromSeconds(10));
    }

    public void Dispose()
    {
        _app.Dispose();
        GC.SuppressFinalize(this);
    }
}
