using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System.IO;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>UI 要素のデバッグダンプを取得するテスト。</summary>
public class DebugDumpTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    /// <summary>テストアプリを起動し、初期状態をセットアップする。</summary>
    public DebugDumpTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    /// <summary>デスクトップおよびアプリケーション内の全UI要素を列挙し、テキストファイルに出力する。</summary>
    [Fact]
    public void DumpAllElements()
    {
        var lines = new List<string>();
        
        // 1. Desktop level windows
        lines.Add("=== Desktop Level Windows ===");
        var desktop = _app.Automation.GetDesktop();
        var allWindows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
        foreach (var w in allWindows)
        {
            lines.Add($"  Window Name='{w.Name}' Class='{w.ClassName}' ProcessId='{w.Properties.ProcessId}'");
        }

        // 2. App top level windows
        lines.Add("=== App Top Level Windows ===");
        try
        {
            var appWindows = _app.Application.GetAllTopLevelWindows(_app.Automation);
            foreach (var w in appWindows)
            {
                lines.Add($"  AppWindow Title='{w.Title}' Name='{w.Name}' Id='{w.AutomationId}'");
                
                // Content of each window
                lines.Add($"  --- Content of {w.Title} ---");
                var descendants = w.FindAllDescendants();
                foreach (var el in descendants)
                {
                    var id = el.AutomationId;
                    if (!string.IsNullOrEmpty(id) && !id.StartsWith("PART_"))
                    {
                        lines.Add($"    Type={el.ControlType} Name='{el.Name}' Id='{id}'");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add($"Error getting app windows: {ex.Message}");
        }

        var outPath = Path.Combine(Path.GetTempPath(), "ui_dump_full.txt");
        File.WriteAllLines(outPath, lines);
        Assert.True(File.Exists(outPath));
    }

    public void Dispose() => _app?.Dispose();
}
