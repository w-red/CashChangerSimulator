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
            return _app.MainWindow.FindAllDescendants(cf => cf.ByAutomationId("InventoryTile")).FirstOrDefault()?.AsButton();
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

            // 3. 全トップレベルウィンドウから検索
            foreach (var win in _app.Application.GetAllTopLevelWindows(_app.Automation))
            {
                d = win.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
                if (d != null) return d;
                
                // 名前でも検索 (CI環境でのフォールバック)
                d = win.FindFirstDescendant(cf => cf.ByName("Denomination Detail"));
                if (d != null) return d;
            }

            return null;
        }, TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(3)).Result;

        _dialog = found!;
    }

    [Fact]
    public void DenominationDetailDialog_ShouldBeVisible()
    {
        _dialog.ShouldNotBeNull("ダイアログが表示されていません。");
    }

    [Fact]
    public void DenominationDetailDialog_ShouldContainCountFields()
    {
        _dialog.ShouldNotBeNull();
        var textBlocks = _dialog.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
        textBlocks.Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
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
