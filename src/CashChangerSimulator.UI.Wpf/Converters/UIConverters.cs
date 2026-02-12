using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.UI.Wpf.Converters;

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

public class TransactionTypeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TransactionType type)
        {
            return type switch
            {
                TransactionType.Deposit => "📥",
                TransactionType.Dispense => "📤",
                TransactionType.Refill => "➕",
                TransactionType.Collection => "➖",
                TransactionType.Adjustment => "🔧",
                _ => "❓"
            };
        }
        return "❓";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
