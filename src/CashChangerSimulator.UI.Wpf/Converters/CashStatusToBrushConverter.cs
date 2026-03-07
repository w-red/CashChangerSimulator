using CashChangerSimulator.Core.Monitoring;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>在庫ステータス（CashStatus）を表示用の色（Brush）に変換するコンバーター。</summary>
/// <remarks>
/// 在庫の「空」「ニアエンプティ」「正常」「満杯近し」「満杯」といった論理的状態を、
/// UI 上で直感的に識別できるよう特定の色にマッピングします。
/// </remarks>
public class CashStatusToBrushConverter : IValueConverter
{
    /// <summary>在庫ステータスを対応する色（Brush）に変換します。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is CashStatus status
            ? status switch
            {
                CashStatus.Empty => Brushes.IndianRed,
                CashStatus.NearEmpty => Brushes.Orange,
                CashStatus.NearFull => Brushes.SkyBlue,
                CashStatus.Full => Brushes.DodgerBlue,
                CashStatus.Normal => Brushes.MediumSeaGreen,
                _ => Brushes.Gray
            }
            : Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
