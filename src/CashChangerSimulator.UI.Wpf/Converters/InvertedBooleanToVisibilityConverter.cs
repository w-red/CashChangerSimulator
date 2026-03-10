using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>論理値（bool）を反転させて Visibility 属性に変換する XAML 用のコンバーター。</summary>
/// <remarks>
/// `true` を `Collapsed` に、`false` を `Visible` に変換します。
/// 特定のフラグが「立っていない」時にのみ要素を表示させたい場合に使用します。
/// </remarks>
internal class InvertedBooleanToVisibilityConverter : IValueConverter
{
    /// <summary>論理値を反転して Visibility に変換します。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
