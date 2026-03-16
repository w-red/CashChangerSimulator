using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>メインウィンドウの表示と操作を制御する UI 要素。</summary>
internal partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = DIContainer.Resolve<MainViewModel>();
        DataContext = _viewModel;

        // Navigation logic
        _viewModel.CurrentUIMode
            .Subscribe(mode => NavigateToMode(mode))
            .AddTo(_disposables);
    }

    private readonly CompositeDisposable _disposables = [];

    private void NavigateToMode(UIMode mode)
    {
        if (MainFrame == null) return;

        switch (mode)
        {
            case UIMode.Standard:
            default:
                MainFrame.Navigate(new StandardSimulationPage { DataContext = _viewModel });
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _disposables.Dispose();
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
