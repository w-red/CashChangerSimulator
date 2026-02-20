using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using R3;
using System.Collections.ObjectModel;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 在庫表示とデバイスステータスを管理する ViewModel。
/// </summary>
public class InventoryViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly Services.CurrencyMetadataProvider _metadataProvider;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly CompositeDisposable _disposables = [];

    public ObservableCollection<DenominationViewModel> Denominations { get; } = [];
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }
    public ReactiveProperty<bool> IsJammed { get; }
    public ReactiveProperty<bool> IsOverlapped { get; }
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];
    public ReactiveCommand OpenSettingsCommand { get; }

    public InventoryViewModel(
        Inventory inventory,
        TransactionHistory history,
        OverallStatusAggregator aggregator,
        ConfigurationProvider configProvider,
        MonitorsProvider monitorsProvider,
        Services.CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController)
    {
        _inventory = inventory;
        _history = history;
        _statusAggregator = aggregator;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _hardwareStatusManager = hardwareStatusManager;

        IsJammed = _hardwareStatusManager.IsJammed;
        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        OverallStatus = _statusAggregator.DeviceStatus;
        FullStatus = _statusAggregator.FullStatus;

        // Denominations initialization
        foreach (var monitor in monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
            string? displayName = null;
            var currentConfig = _configProvider.Config;
            if (currentConfig.Inventory.TryGetValue(currentConfig.CurrencyCode, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                displayName = setting.DisplayName;
            }

            Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, depositController, monitor, displayName));
        }

        _history.Added
            .Subscribe(entry =>
            {
                void AddEntry()
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                }

                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(AddEntry);
                }
                else
                {
                    AddEntry();
                }
            })
            .AddTo(_disposables);

        OpenSettingsCommand = new ReactiveCommand().AddTo(_disposables);
        OpenSettingsCommand.Subscribe(_ =>
        {
            var settingsWindow = new SettingsWindow(_configProvider, _monitorsProvider, _metadataProvider)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        });
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
