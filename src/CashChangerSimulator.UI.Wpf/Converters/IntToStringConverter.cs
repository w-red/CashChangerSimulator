using System.Globalization;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>
/// TextBox の空文字と数値型の相互変換をサポートするコンバーター。
/// 空文字の場合は 0 を返し、バインドエラーを防ぎます。
/// </summary>
public class IntToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() ?? "0";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var str = value as string;
        if (string.IsNullOrWhiteSpace(str))
        {
            return 0;
        }

        if (int.TryParse(str, out var result))
        {
            return result;
        }

        return 0;
    }
}
