using CashChangerSimulator.Core.Transactions;
using MaterialDesignThemes.Wpf;
using System.Globalization;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>取引タイプを対応するアイコン（PackIconKind）に変換するコンバーター。</summary>
internal class TransactionTypeToSymbolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TransactionType type
            ? type switch
            {
                TransactionType.Deposit => PackIconKind.ArrowDownBoldCircleOutline,
                TransactionType.Dispense => PackIconKind.ArrowUpBoldCircleOutline,
                TransactionType.Refill => PackIconKind.TrayArrowDown,
                TransactionType.Collection => PackIconKind.TrayArrowUp,
                TransactionType.Adjustment => PackIconKind.Tools,
                TransactionType.DataEvent => PackIconKind.BellAlertOutline,
                TransactionType.Open => PackIconKind.Power,
                TransactionType.Close => PackIconKind.PowerOff,
                TransactionType.Claim => PackIconKind.Lock,
                TransactionType.Release => PackIconKind.LockOpen,
                TransactionType.Error => PackIconKind.AlertCircleOutline,
                _ => PackIconKind.HelpCircleOutline
            }
            : PackIconKind.HelpCircleOutline;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
