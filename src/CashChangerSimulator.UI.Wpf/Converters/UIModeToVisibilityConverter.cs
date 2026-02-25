using CashChangerSimulator.Core.Configuration;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>現在の UIMode が、パラメータで指定された UIMode と一致するか判定するコンバーター。</summary>
public class UIModeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UIMode currentMode && parameter is string targetModeStr)
        {
            if (Enum.TryParse<UIMode>(targetModeStr, out var targetMode))
            {
                return currentMode == targetMode ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
