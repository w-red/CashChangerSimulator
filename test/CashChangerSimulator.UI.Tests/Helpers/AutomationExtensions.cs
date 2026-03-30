using System.Text;
using FlaUI.Core.AutomationElements;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>UI オートメーション要素の探索とデバッグ出力を補助する拡張メソッド群。</summary>
public static class AutomationExtensions
{
    /// <summary>指定されたオートメーション要素以下の子要素ツリーを再帰的に走査し、StringBuilder に書式化して出力します。</summary>
    public static void CaptureElements(this AutomationElement element, int depth, StringBuilder sb)
    {
        if (depth > 8) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var children = element.FindAllChildren();
            foreach (var child in children)
            {
                sb.AppendLine($"{indent} - {child.ControlType} Name:\"{child.Name}\", ID:\"{child.Properties.AutomationId}\", Off:\"{child.IsOffscreen}\", Rect:\"{child.BoundingRectangle}\"");
                CaptureElements(child, depth + 1, sb);
            }
        }
        catch { }
    }

    /// <summary>指定されたオートメーション要素以下の子要素ツリーを再帰的に走査し、TextWriter に書式化して出力します。</summary>
    public static void CaptureElements(this AutomationElement element, int depth, System.IO.TextWriter writer)
    {
        var sb = new StringBuilder();
        CaptureElements(element, depth, sb);
        writer.WriteLine(sb.ToString());
    }
}
