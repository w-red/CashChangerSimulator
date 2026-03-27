using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
using R3;
using System.Windows;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>メインウィンドウの表示と操作を制御する UI 要素。</summary>
internal partial class MainWindow : Window
{
    private readonly IViewModelFactory _viewModelFactory;
    private readonly MainViewModel _viewModel;

    /// <summary>MainWindow の新しいインスタンスを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        
        // [STABILITY] Explicitly set AutomationId via code to ensure it's available early
        System.Windows.Automation.AutomationProperties.SetAutomationId(this, "MainWindowRoot");
        
        _viewModelFactory = DIContainer.Resolve<IViewModelFactory>();
        _viewModel = DIContainer.Resolve<MainViewModel>();
        DataContext = _viewModel;

        // Navigation logic
        _viewModel.CurrentUIMode
            .Subscribe(mode => NavigateToMode(mode))
            .AddTo(_disposables);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // [STABILITY] Wait for UI and Automation Tree to settle before heavy initialization
        await Task.Delay(1000);

        // Async initialization (HotStart etc.)
        if (_viewModel != null)
        {
            await _viewModel.InitializeAsync();
        }

        // [TEST HARNESS] Auto-open device if requested via environment variable
        // [STABILITY] Check if already connected (e.g. by HotStart) to prevent double-open
        if (Environment.GetEnvironmentVariable("TEST_AUTO_OPEN_DEVICE") == "True")
        {
            if (_viewModel?.Facade?.Status?.IsConnected?.Value == false && 
                _viewModel?.Inventory?.OpenCommand?.CanExecute() == true)
            {
                _viewModel.Inventory.OpenCommand.Execute(Unit.Default);
            }
        }
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
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
