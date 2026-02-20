using CashChangerSimulator.Core.Models;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>在庫ステータスを対応する色（Brush）に変換するコンバーター。</summary>
public class CashStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is CashStatus status)
        {
            return status switch
            {
                CashStatus.Empty => Brushes.IndianRed,
                CashStatus.NearEmpty => Brushes.Orange,
                CashStatus.NearFull => Brushes.SkyBlue,
                CashStatus.Full => Brushes.DodgerBlue,
                CashStatus.Normal => Brushes.MediumSeaGreen,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
