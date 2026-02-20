using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>反転した論理値を Visibility 属性に変換するコンバーター。</summary>
public class InvertedBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
