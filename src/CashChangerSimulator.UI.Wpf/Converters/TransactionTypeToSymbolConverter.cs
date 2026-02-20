using CashChangerSimulator.Core.Models;
using MaterialDesignThemes.Wpf;
using System.Globalization;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>取引タイプを対応するアイコン（PackIconKind）に変換するコンバーター。</summary>
public class TransactionTypeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TransactionType type)
        {
            return type switch
            {
                TransactionType.Deposit => PackIconKind.ArrowDownBoldCircleOutline,
                TransactionType.Dispense => PackIconKind.ArrowUpBoldCircleOutline,
                TransactionType.Refill => PackIconKind.TrayArrowDown,
                TransactionType.Collection => PackIconKind.TrayArrowUp,
                TransactionType.Adjustment => PackIconKind.Tools,
                _ => PackIconKind.HelpCircleOutline
            };
        }
        return PackIconKind.HelpCircleOutline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
