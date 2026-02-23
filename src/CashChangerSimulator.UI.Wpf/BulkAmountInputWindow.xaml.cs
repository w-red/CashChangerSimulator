using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>金種指定入出金用のダイアログウィンドウ。</summary>
public partial class BulkAmountInputWindow : Window
{
    public BulkAmountInputWindow(string title, string? buttonText = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        Title = title;
        if (!string.IsNullOrEmpty(buttonText))
        {
            ConfirmButton.Content = buttonText;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
