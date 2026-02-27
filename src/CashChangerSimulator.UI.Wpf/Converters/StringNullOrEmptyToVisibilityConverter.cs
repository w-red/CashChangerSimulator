using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>
/// 文字列がnullまたは空の場合にCollapsedまたはHiddenを返し、値がある場合はVisibleを返すコンバーター。
/// </summary>
public class StringNullOrEmptyToVisibilityConverter : IValueConverter
{
    public Visibility NullOrEmptyValue { get; set; } = Visibility.Collapsed;
    public Visibility HasValueValue { get; set; } = Visibility.Visible;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        if (string.IsNullOrEmpty(str))
        {
            return NullOrEmptyValue;
        }

        return HasValueValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
