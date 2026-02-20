using CashChangerSimulator.UI.Wpf.ViewModels;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>メインウィンドウの表示と操作を制御する UI 要素。</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = DIContainer.Resolve<MainViewModel>();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}