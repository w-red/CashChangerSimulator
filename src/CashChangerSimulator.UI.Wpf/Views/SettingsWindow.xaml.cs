using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
using System.Windows;

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
            DIContainer.Resolve<Services.CurrencyMetadataProvider>());
        DataContext = _viewModel;
    }

    /// <summary>Save ボタンクリック時に保存成功を確認してダイアログを閉じる。</summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SaveSucceeded.Value)
        {
            DialogResult = true;
            Close();
        }
    }
}
