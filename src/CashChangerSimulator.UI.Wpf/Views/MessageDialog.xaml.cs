using System.Windows.Controls;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// DialogHost で表示するための汎用メッセージダイアログ。
/// </summary>
public partial class MessageDialog : UserControl
{
    public MessageDialog()
    {
        InitializeComponent();
        this.DataContext = this;
    }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
