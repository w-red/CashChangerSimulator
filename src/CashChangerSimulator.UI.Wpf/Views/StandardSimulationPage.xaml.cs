using System.Windows; // Added for Visibility
using System.Windows.Controls;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// Interaction logic for StandardSimulationPage.xaml
/// </summary>
public partial class StandardSimulationPage : Page
{
    /// <summary>StandardSimulationPage の新しいインスタンスを初期化する。</summary>
    public StandardSimulationPage()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 1000px 未満ならサイドバーを非表示にする（リキッド・レスポンシブ対応）
        if (e.NewSize.Width < 1000)
        {
            SidebarContent.Visibility = Visibility.Collapsed;
        }
        else
        {
            SidebarContent.Visibility = Visibility.Visible;
        }
    }
}
