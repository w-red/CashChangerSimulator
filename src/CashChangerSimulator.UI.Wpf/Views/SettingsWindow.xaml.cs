using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf.Services;
using System.Windows;
using R3;

namespace CashChangerSimulator.UI.Wpf.Views;

/// <summary>設定画面の表示と操作を制御する UI 要素。</summary>
internal partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    /// <summary>ViewModel を初期化して画面を生成する。</summary>
    public SettingsWindow(IViewModelFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        InitializeComponent();
        _viewModel = factory.CreateSettingsViewModel();
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

