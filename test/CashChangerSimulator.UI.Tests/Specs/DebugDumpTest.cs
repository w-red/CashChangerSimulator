using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using System.IO;

namespace CashChangerSimulator.UI.Tests.Specs;

/// <summary>UI 要素のデバッグダンプを取得するテスト。</summary>
public class DebugDumpTest : IDisposable
{
    private readonly CashChangerTestApp _app;

    public DebugDumpTest()
    {
        _app = new CashChangerTestApp();
        _app.Launch();
    }

    [Fact]
    public void DumpAllElements()
    {
        var window = _app.MainWindow;
        Assert.NotNull(window);
        window.SetForeground();
        Thread.Sleep(2000);

        var lines = new List<string>();
        var all = window.FindAllDescendants();
        lines.Add($"=== Found {all.Length} total elements ===");
        foreach (var el in all)
        {
            try
            {
                var id = el.AutomationId;
                if (!string.IsNullOrEmpty(id) && id != "PART_EditableTextBox" 
                    && !id.StartsWith("PART_") && id != "PageUp" && id != "PageDown")
                {
                    lines.Add($"  Type={el.ControlType} Name='{el.Name}' AutomationId='{id}' Class='{el.ClassName}'");
                }
            }
            catch { }
        }
        
        // Also dump all buttons regardless of AutomationId
        lines.Add("=== All Buttons ===");
        var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        int idx = 0;
        foreach (var btn in buttons)
        {
            try
            {
                var children = btn.FindAllChildren();
                var childNames = string.Join(", ", children.Select(c => $"{c.ControlType}:'{c.Name}'"));
                lines.Add($"  [{idx}] Name='{btn.Name}' Id='{btn.AutomationId}' Children=[{childNames}]");
            }
            catch { }
            idx++;
        }

        var outPath = Path.Combine(Path.GetTempPath(), "ui_dump_full.txt");
        File.WriteAllLines(outPath, lines);
        Assert.True(File.Exists(outPath));
    }

    public void Dispose() => _app?.Dispose();
}
