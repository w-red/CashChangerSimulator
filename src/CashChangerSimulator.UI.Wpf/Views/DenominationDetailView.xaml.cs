using System.Windows.Controls;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// DenominationDetailView.xaml の相互作用ロジック
/// </summary>
public partial class DenominationDetailView : UserControl
{
    public DenominationDetailView()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var parentWin = System.Windows.Window.GetWindow(this);
        if (parentWin != null && parentWin != System.Windows.Application.Current.MainWindow)
        {
            parentWin.Close();
        }
        else
        {
            // DialogHost を通じてダイアログを閉じる
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
