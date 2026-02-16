using System.Windows;
using CashChangerSimulator.UI.Wpf.ViewModels;

namespace CashChangerSimulator.UI.Wpf;

/// <summary>設定画面の表示と操作を制御する UI 要素。</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>各プロバイダーを注入し、ViewModel を初期化して画面を生成する。</summary>
    public SettingsWindow(ConfigurationProvider configProvider, MonitorsProvider monitorsProvider, Services.CurrencyMetadataProvider metadataProvider)
    {
        InitializeComponent();
        _viewModel = new SettingsViewModel(configProvider, monitorsProvider, metadataProvider);
        DataContext = _viewModel;
    }

    /// <summary>Save ボタンクリック時に保存成功を確認してダイアログを閉じる。</summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SaveSucceeded)
        {
            DialogResult = true;
            Close();
        }
    }
}
