using System.Text;
using FlaUI.Core.AutomationElements;

namespace CashChangerSimulator.UI.Tests.Helpers;

public static class AutomationExtensions
{
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

    public static void CaptureElements(this AutomationElement element, int depth, System.IO.TextWriter writer)
    {
        var sb = new StringBuilder();
        CaptureElements(element, depth, sb);
        writer.WriteLine(sb.ToString());
    }
}
