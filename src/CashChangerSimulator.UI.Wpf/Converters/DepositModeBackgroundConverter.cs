using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>入金モードかどうかに応じて背景色を切り替えるコンバーター。</summary>
public class DepositModeBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isInDepositMode && isInDepositMode)
        {
            return new SolidColorBrush(Color.FromArgb(40, 187, 134, 252)); // PrimaryColor with low opacity
        }
        return Brushes.Transparent;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
