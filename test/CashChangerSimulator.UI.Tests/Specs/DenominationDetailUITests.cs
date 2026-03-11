using System.IO;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Tools;
using Shouldly;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>
/// 金種詳細ダイアログの表示・内容・閉じる動作を検証する UI テスト。
/// xUnit はコンストラクタを各テストごとに呼ぶため、アプリ起動は各テストで独立している。
/// ただしダイアログを開くトリガーは一度だけ書き込む（ViewModel 側ポーリングが対応）。
/// </summary>
public class DenominationDetailUITests : IDisposable
{
    private readonly CashChangerTestApp _app;
    private readonly string _triggerPath;
    private readonly AutomationElement _dialog;

    /// <summary>ファイルトリガーフックを有効にしてテストアプリを起動し、ダイアログを開く。</summary>
    public DenominationDetailUITests()
    {
        _triggerPath = Path.Combine(Path.GetTempPath(), $"denomination_detail_{Guid.NewGuid():N}.trigger");
        _app = new CashChangerTestApp();
        _app.Launch();

        // アプリが完全に起動するまで待機（BillDenominations の初期化含む）
        Thread.Sleep(5000);

        // テスト専用ボタン（InventoryControl.xaml に追加された透明 1x1 ボタン）を探して InvokePattern で呼び出す
        var testButton = _app.MainWindow?.FindFirstDescendant(cf =>
            cf.ByAutomationId("ShowFirstDenominationDetailTestButton"))?.AsButton();

        Console.WriteLine($"[DEBUG] testButton found: {testButton != null}");
        testButton.ShouldNotBeNull("ShowFirstDenominationDetailTestButton ボタンが UI ツリーに存在するべきです。");
        Console.WriteLine($"[DEBUG] Button IsEnabled: {testButton.IsEnabled}");
        Console.WriteLine($"[DEBUG] Button IsOffscreen: {testButton.IsOffscreen}");
        Console.WriteLine($"[DEBUG] Clicking testButton...");
        testButton.Click();
        Console.WriteLine("[DEBUG] Clicked.");
        Thread.Sleep(2000); // UIスレッドがダイアログを描画するまで待機

        // ダイアログが現れるまで待機（最大 15 秒）
        // Show() による子 Window はトップレベルウィンドウとして出現する
        var found = Retry.WhileNull(() =>
        {
            // アプリ内の全トップレベルウィンドウを検索
            var windows = _app.Application.GetAllTopLevelWindows(_app.Automation);
            foreach (var win in windows)
            {
                try
                {
                    if (win.AutomationId == "DenominationDetailDialogView") return win;
                }
                catch { }
                try
                {
                    if (win.Title == "Denomination Detail") return win;
                }
                catch { }
                // DenominationDetailDialogView を子孫から検索
                var inner = win.FindFirstDescendant(cf => cf.ByAutomationId("DenominationDetailDialogView"));
                if (inner != null) return inner;
            }

            // デスクトップ直下でも検索
            var desktop = _app.Automation.GetDesktop();
            foreach (var child in desktop.FindAllChildren())
            {
                try
                {
                    if (child.AutomationId == "DenominationDetailDialogView") return child;
                }
                catch { }
                try
                {
                    if (child.Name == "Denomination Detail") return child;
                }
                catch { }
            }
            return null;
        }, TimeSpan.FromSeconds(15)).Result;

        _dialog = found!;
    }

    // ────────────────────────────────────────────────────
    // テスト
    // ────────────────────────────────────────────────────

    /// <summary>金種詳細ダイアログが画面上に表示されることを確認する。</summary>
    [Fact]
    public void DenominationDetailDialog_ShouldBeVisible()
    {
        _dialog.ShouldNotBeNull("金種詳細ダイアログ (DenominationDetailDialogView) が表示されているべきです。");
        _dialog.IsOffscreen.ShouldBeFalse("ダイアログは画面上に表示されているべきです。");
    }

    /// <summary>ダイアログ内に枚数を示す TextBlock が複数存在することを確認する。</summary>
    [Fact]
    public void DenominationDetailDialog_ShouldContainCountFields()
    {
        _dialog.ShouldNotBeNull();

        var textBlocks = _dialog.FindAllDescendants(cf =>
            cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));

        // リサイクル / 回収 / リジェクト ＋ タイトル等で最低 3 つ
        textBlocks.Length.ShouldBeGreaterThanOrEqualTo(3,
            "リサイクル/回収/リジェクトの各枚数 TextBlock が存在するべきです。");
    }

    /// <summary>閉じるボタンをクリックするとダイアログが閉じることを確認する。</summary>
    [Fact]
    public void DenominationDetailDialog_CloseButton_ShouldDismissDialog()
    {
        _dialog.ShouldNotBeNull();
        Thread.Sleep(500); // 描画安定用

        // ダイアログ内の Button を探す（MaterialDesign の CloseDialogCommand ボタン）
        var closeButton = _dialog
            .FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button))
            .FirstOrDefault();

        closeButton.ShouldNotBeNull("ダイアログに閉じるボタンが存在するべきです。");

        if (closeButton.Patterns.Invoke.IsSupported)
            closeButton.Patterns.Invoke.Pattern.Invoke();
        else
            closeButton.Click();

        // ダイアログが消えるまで待機（最大 5 秒）
        Retry.WhileTrue(() =>
        {
            var remaining = _app.MainWindow?.FindFirstDescendant(cf =>
                cf.ByAutomationId("DenominationDetailDialogView"));
            return remaining != null && !remaining.IsOffscreen;
        }, TimeSpan.FromSeconds(5));

        // ダイアログが非表示 or 消えていることを確認
        var afterClose = _app.MainWindow?.FindFirstDescendant(cf =>
            cf.ByAutomationId("DenominationDetailDialogView"));
        (afterClose == null || afterClose.IsOffscreen).ShouldBeTrue(
            "閉じるボタンを押した後、ダイアログは消えているべきです。");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        try { if (File.Exists(_triggerPath)) File.Delete(_triggerPath); } catch { }
        _app.Dispose();
        GC.SuppressFinalize(this);
    }
}
