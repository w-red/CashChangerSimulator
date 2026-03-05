using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>WPF アプリケーションのリソースから文字列を取得するためのヘルパークラス。</summary>
public static class ResourceHelper
{
    /// <summary>指定されたキーのリソースを文字列として取得します。</summary>
    /// <param name="key">リソースキー。</param>
    /// <param name="fallback">リソースが見つからない場合や null の場合のフォールバック文字列。</param>
    /// <returns>取得された文字列、またはフォールバック値。</returns>
    public static string GetAsString(string key, string fallback = "") => Application.Current?.Resources[key] as string ?? fallback;
}
