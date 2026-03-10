using System.Windows; // Added for Visibility
using System.Windows.Controls;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>
/// Interaction logic for StandardSimulationPage.xaml
/// </summary>
internal partial class StandardSimulationPage : Page
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
        SidebarContent.Visibility = e.NewSize.Width < 1000 ? Visibility.Collapsed : Visibility.Visible;
    }
}
