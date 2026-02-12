using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

public class MainViewModel : IDisposable, INotifyPropertyChanged, INotifyDataErrorInfo
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

    // 双方向バインディング用のプロパティ（バリデーション付き）
    private string _dispenseAmountInput = "";
    public string DispenseAmountInput
    {
        get => _dispenseAmountInput;
        set
        {
            if (_dispenseAmountInput != value)
            {
                _dispenseAmountInput = value;
                OnPropertyChanged();
                ValidateDispenseAmount(value);
                DispenseAmountText.Value = value;
            }
        }
    }

    // 内部ロジック用
    public ReactiveProperty<string> DispenseAmountText { get; }

    // コマンド
    public ReactiveCommand DispenseCommand { get; }

    // INotifyDataErrorInfo
    private readonly Dictionary<string, List<string>> _errors = new();
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    public bool HasErrors => _errors.Count > 0;
    public System.Collections.IEnumerable GetErrors(string? propertyName) => 
        _errors.GetValueOrDefault(propertyName ?? "") ?? Enumerable.Empty<string>();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void ValidateDispenseAmount(string text)
    {
        var propertyName = nameof(DispenseAmountInput);
        if (_errors.ContainsKey(propertyName))
        {
            _errors.Remove(propertyName);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        var errors = new List<string>();
        if (!string.IsNullOrWhiteSpace(text))
        {
             if (!int.TryParse(text, out var val)) errors.Add("Enter a valid number");
             else if (val <= 0) errors.Add("Amount must be positive");
             else if (val > TotalAmount.CurrentValue) errors.Add("Insufficient funds");
        }
        
        if (errors.Count > 0)
        {
            _errors[propertyName] = errors;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

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
            
        // Dispense Logic
        DispenseAmountText = new ReactiveProperty<string>("")
            .AddTo(_disposables);

        // エラー状態の変更を監視してコマンドの有効状態を制御
        var errorsChangedObservable = Observable.FromEvent<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>(
            h => (s, e) => h(e),
            h => ErrorsChanged += h,
            h => ErrorsChanged -= h);

        var canDispense = errorsChangedObservable
            .Select(_ => !HasErrors)
            .Prepend(!HasErrors)
            .CombineLatest(DispenseAmountText, (noError, text) => noError && !string.IsNullOrEmpty(text));

        DispenseCommand = canDispense
            .ToReactiveCommand()
            .AddTo(_disposables);
            
        DispenseCommand.Subscribe(_ =>
        {
            if (decimal.TryParse(DispenseAmountText.Value, out var amount))
            {
                DispenseCash(amount);
                DispenseAmountInput = ""; // Clear via wrapper to trigger notification
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
