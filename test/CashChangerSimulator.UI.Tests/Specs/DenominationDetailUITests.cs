using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>
/// 金種詳細ダイアログの表示・内容・閉じる動作を検証する UI テスト。
/// </summary>
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
            Console.WriteLine($"[DIAG] Found {tiles.Length} InventoryTiles");
            return tiles.FirstOrDefault()?.AsButton();
        }, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)).Result;

        inventoryTile.ShouldNotBeNull("InventoryTile が見つかりません。");
        
        // クリックとダイアログ検索をセットで行い、見つかるまで再試行する（空振り対策）
        var found = Retry.WhileNull(() =>
        {
            // まずクリックを試みる
            try
            {
                if (inventoryTile.Patterns.Invoke.IsSupported)
                    inventoryTile.Patterns.Invoke.Pattern.Invoke();
                else
                    inventoryTile.Click();
            }
            catch { }

            // クリック後の描画待ち
            Thread.Sleep(1500);

            if (_app.MainWindow == null) return null;

            // 1. 直接検索
            var d = _app.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
            if (d != null) return d;

            // 2. DialogHost (MainDialogHost) を経由して検索
            var host = _app.MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("MainDialogHost"));
            if (host != null)
            {
                d = host.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
                if (d != null) return d;
            }

            if (d == null)
            {
                // [DIAGNOSTICS] もし見つからない場合、全トップレベルウィンドウの子要素をダンプする
                foreach (var win in _app.Application.GetAllTopLevelWindows(_app.Automation))
                {
                    Console.WriteLine($"[DIAG] Investigating Window: {win.Name} (ID: {win.Properties.AutomationId})");
                    DumpElements(win, 0);
                }
            }

            return d;
        }, TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(3)).Result;

        if (found == null)
        {
            var dump = new System.Text.StringBuilder();
            foreach (var win in _app.Application.GetAllTopLevelWindows(_app.Automation))
            {
                dump.AppendLine($"Window: {win.Name} (ID: {win.Properties.AutomationId})");
                CaptureElements(win, 0, dump);
            }
            throw new System.Exception($"ダイアログ 'DenominationDetailDialogView' が制限時間内に見つかりませんでした。\n[UI TREE SNAPSHOT]\n{dump}\nCI ログの詳細出力を確認してください。");
        }
        _dialog = found;
    }

    private void DumpElements(AutomationElement element, int depth)
    {
        if (depth > 5) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                Console.WriteLine($"[DIAG]{indent} - {child.ControlType} Name:\"{child.Name}\", ID:\"{child.Properties.AutomationId}\"");
                DumpElements(child, depth + 1);
            }
        }
        catch { }
    }

    private void CaptureElements(AutomationElement element, int depth, System.Text.StringBuilder sb)
    {
        if (depth > 3) return; // 例外メッセージが長くなりすぎないよう制限
        var indent = new string(' ', depth * 2);
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                sb.AppendLine($"{indent} - {child.ControlType} Name:\"{child.Name}\", ID:\"{child.Properties.AutomationId}\"");
                CaptureElements(child, depth + 1, sb);
            }
        }
        catch { }
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
        if (closeButton.Patterns.Invoke.IsSupported)
            closeButton.Patterns.Invoke.Pattern.Invoke();
        else
            closeButton.Click();

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
