using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

public class MainViewModel : IDisposable
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly CompositeDisposable _disposables = new();

    public ObservableCollection<DenominationViewModel> Denominations { get; } = new();
    public ReadOnlyReactiveProperty<decimal> TotalAmount { get; }
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = new();

    // 双方向バインディング用のプロパティ
    public ReactiveProperty<string> DispenseAmountText { get; }

    // コマンド
    public ReactiveCommand DispenseCommand { get; }

    public MainViewModel()
    {
        // Load settings from TOML
        var config = ConfigurationLoader.Load();
        
        _inventory = new Inventory();
        foreach (var item in config.Inventory.InitialCounts)
        {
            if (int.TryParse(item.Key, out int denom))
            {
                _inventory.SetCount(denom, item.Value);
            }
        }

        _history = new TransactionHistory();
        _manager = new CashChangerManager(_inventory, _history);

        // Define JPY denominations
        var denominationValues = new[] { 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1 };
        
        var monitors = denominationValues.Select(d =>
        {
            var vm = new DenominationViewModel(_inventory, d);
            Denominations.Add(vm);
            
            // For status monitoring using thresholds from config
            return new CashStatusMonitor(_inventory, d, 
                nearEmptyThreshold: config.Thresholds.NearEmpty, 
                nearFullThreshold: config.Thresholds.NearFull, 
                fullThreshold: config.Thresholds.Full);
        }).ToList();

        _statusAggregator = new OverallStatusAggregator(monitors);
        OverallStatus = _statusAggregator.OverallStatus;

        // Reactive Total Amount
        TotalAmount = _inventory.Changed
            .Select(_ => _inventory.CalculateTotal())
            .ToReadOnlyReactiveProperty(_inventory.CalculateTotal())
            .AddTo(_disposables);

        // Experience: Subscribe to history to show in UI
        _history.Added
            .Subscribe(entry =>
            {
                // UIスレッドでの更新が必要な場合、SynchronizationContext経由で行われることを期待
                // R3 の ObserveOn を使うか、App.xaml.cs で Provider を設定する
                 App.Current.Dispatcher.Invoke(() =>
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                });
            })
            .AddTo(_disposables);
            
        // Dispense Logic
        DispenseAmountText = new ReactiveProperty<string>("")
            .AddTo(_disposables);

        DispenseCommand = DispenseAmountText
            .Select(text => decimal.TryParse(text, out var val) && val > 0)
            .ToReactiveCommand()
            .AddTo(_disposables);
            
        DispenseCommand.Subscribe(_ =>
        {
            if (decimal.TryParse(DispenseAmountText.Value, out var amount))
            {
                DispenseCash(amount);
                DispenseAmountText.Value = ""; // Clear after dispense
            }
        });
    }

    private void DispenseCash(decimal amount)
    {
        try
        {
            _manager.Dispense(amount);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _statusAggregator.Dispose();
    }
}

public class DenominationViewModel
{
    private readonly Inventory _inventory;
    public int Value { get; }
    public string Name => Value >= 1000 ? $"{Value / 1000}千円札" : $"{Value}円玉";
    public ReadOnlyReactiveProperty<int> Count { get; }
    
    public ReactiveCommand AddCommand { get; }
    public ReactiveCommand RemoveCommand { get; }

    public DenominationViewModel(Inventory inventory, int value)
    {
        _inventory = inventory;
        Value = value;
        Count = _inventory.Changed
            .Where(d => d == value)
            .Select(_ => _inventory.GetCount(value))
            .ToReadOnlyReactiveProperty(_inventory.GetCount(value));
            
        AddCommand = new ReactiveCommand();
        AddCommand.Subscribe(_ => _inventory.Add(Value, 1));
        
        RemoveCommand = new ReactiveCommand();
        RemoveCommand.Subscribe(_ => _inventory.Add(Value, -1));
    }
}
