using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CashChangerSimulator.Core.Models;
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

    public MainViewModel()
    {
        _inventory = new Inventory();
        _history = new TransactionHistory();
        _manager = new CashChangerManager(_inventory, _history);

        // Define JPY denominations
        var denominationValues = new[] { 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1 };
        
        var monitors = denominationValues.Select(d =>
        {
            var vm = new DenominationViewModel(_inventory, d);
            Denominations.Add(vm);
            
            // For status monitoring
            return new CashStatusMonitor(_inventory, d, 5, 90, 100);
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                });
            })
            .AddTo(_disposables);
    }

    public void AddCash(int denomination, int count)
    {
        _manager.Deposit(new Dictionary<int, int> { { denomination, count } });
    }

    public void DispenseCash(decimal amount)
    {
        try
        {
            _manager.Dispense(amount);
        }
        catch (Exception ex)
        {
            // Error handling will be caught by UI or a property
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

    public DenominationViewModel(Inventory inventory, int value)
    {
        _inventory = inventory;
        Value = value;
        Count = _inventory.Changed
            .Where(d => d == value)
            .Select(_ => _inventory.GetCount(value))
            .ToReadOnlyReactiveProperty(_inventory.GetCount(value));
    }
}
