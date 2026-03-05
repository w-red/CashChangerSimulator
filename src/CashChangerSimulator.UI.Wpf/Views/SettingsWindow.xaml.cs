using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using System.Windows;
using R3;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>設定画面の表示と操作を制御する UI 要素。</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>DI コンテナからプロバイダーを解決し、ViewModel を初期化して画面を生成する。</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(
            DIContainer.Resolve<ConfigurationProvider>(),
            DIContainer.Resolve<MonitorsProvider>(),
            DIContainer.Resolve<CurrencyMetadataProvider>());
        DataContext = _viewModel;

        // 保存成功時にウィンドウを閉じる
        _viewModel.SaveSucceeded
            .Where(x => x)
            .Subscribe(_ =>
            {
                DialogResult = true;
                Close();
            });
    }
}

