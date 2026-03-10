using System.Globalization;
using System.Windows.Data;

namespace CashChangerSimulator.UI.Wpf.Converters;

/// <summary>論理値（bool）を反転させる XAML 用のコンバーター。</summary>
/// <remarks>
/// `true` を `false` に、`false` を `true` に変換します。
/// UI 要素の有効/無効状態や可視性を、条件の否定に基づいて制御する場合に使用します。
/// </remarks>
internal class InverseBooleanConverter : IValueConverter
{
    /// <summary>論理値を反転します。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
