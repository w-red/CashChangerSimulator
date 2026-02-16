using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Threading;
using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// アプリケーションのメイン画面を制御する ViewModel。
/// 在庫表示、払出操作、履歴管理、設定画面の起動などを担当する。
/// </summary>
public class MainViewModel : IDisposable, INotifyPropertyChanged, INotifyDataErrorInfo
{
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly CashChangerManager _manager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly ConfigurationProvider _configProvider;
    private readonly MonitorsProvider _monitorsProvider;
    private readonly Services.CurrencyMetadataProvider _metadataProvider;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>画面に表示する金種別情報のリスト。</summary>
    public ObservableCollection<DenominationViewModel> Denominations { get; } = new();
    private readonly ReactiveProperty<decimal> _totalAmount;
    /// <summary>在庫の合計金額（ReactiveProperty 版）。</summary>
    public ReadOnlyReactiveProperty<decimal> TotalAmount { get; }
    /// <summary>在庫の合計金額（WPF バインディング用の プレーンプロパティ）。</summary>
    public decimal TotalAmountCurrency => _totalAmount.Value;
    /// <summary>全金種のステータスを統合したデバイス全体のステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>最近の取引履歴のリスト。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = new();
    /// <summary>設定画面を開くコマンド。</summary>
    public RelayCommand OpenSettingsCommand { get; }

    /// <summary>ユーザーが入力した払出金額（文字列）。</summary>
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

    /// <summary>払出処理用の内部状態を保持する ReactiveProperty。</summary>
    public ReactiveProperty<string> DispenseAmountText { get; }

    /// <summary>払出処理を実行するコマンド。</summary>
    public ReactiveCommand DispenseCommand { get; }

    private readonly Dictionary<string, List<string>> _errors = new();

    /// <summary>バリデーションエラーが変更されたときに発生するイベント。</summary>
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    /// <summary>現在バリデーションエラーが存在するかどうか。</summary>
    public bool HasErrors => _errors.Count > 0;
    /// <summary>指定したプロパティのエラー一覧を取得する。</summary>
    public System.Collections.IEnumerable GetErrors(string? propertyName) => 
        _errors.GetValueOrDefault(propertyName ?? "") ?? Enumerable.Empty<string>();

    /// <summary>プロパティ値が変更されたときに発生するイベント。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>プロパティ変更通知を発生させる。</summary>
    protected void OnPropertyChanged([CallerMemberName] string? name = null) 
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>指定されたテキストを元に払出金額の妥当性を検証する。</summary>
    private void ValidateDispenseAmount(string text)
    {
        var propertyName = nameof(DispenseAmountInput);
        if (_errors.ContainsKey(propertyName))
        {
            _errors.Remove(propertyName);
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        var validationErrors = new List<string>();
        if (!string.IsNullOrWhiteSpace(text))
        {
             if (!decimal.TryParse(text, out var val)) validationErrors.Add("Enter a valid number");
             else if (val <= 0) validationErrors.Add("Amount must be positive");
             else if (val > TotalAmount.CurrentValue) validationErrors.Add("Insufficient funds");
        }
        
        if (validationErrors.Count > 0)
        {
            _errors[propertyName] = validationErrors;
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 各種プロバイダーを注入して MainViewModel を初期化する。
    /// </summary>
    public MainViewModel(
        Inventory inventory,
        TransactionHistory history,
        CashChangerManager manager,
        MonitorsProvider monitorsProvider,
        OverallStatusAggregatorProvider aggregatorProvider,
        ConfigurationProvider configProvider,
        Services.CurrencyMetadataProvider metadataProvider)
    {
        _inventory = inventory;
        _history = history;
        _manager = manager;
        _statusAggregator = aggregatorProvider.Aggregator;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;

        // Initialize Denominations
        foreach (var monitor in monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
            string? displayName = null;
            if (_configProvider.Config.Inventory.Denominations.TryGetValue(keyStr, out var setting))
            {
                displayName = setting.DisplayName;
            }

            Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, displayName));
        }

        OverallStatus = _statusAggregator.OverallStatus;

        // Reactive Total Amount
        _totalAmount = new ReactiveProperty<decimal>(_inventory.CalculateTotal()).AddTo(_disposables);
        TotalAmount = _totalAmount;

        _inventory.Changed
            .Subscribe(_ =>
            {
                var total = _inventory.CalculateTotal();
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        _totalAmount.Value = total;
                        OnPropertyChanged(nameof(TotalAmountCurrency));
                    });
                }
                else
                {
                    _totalAmount.Value = total;
                    OnPropertyChanged(nameof(TotalAmountCurrency));
                }
            })
            .AddTo(_disposables);

        _history.Added
            .Subscribe(entry =>
            {
                Action action = () =>
                {
                    RecentTransactions.Insert(0, entry);
                    if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                };

                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(action);
                }
                else
                {
                    action();
                }
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

        // Settings Command
        OpenSettingsCommand = new RelayCommand(() =>
        {
            var settingsWindow = new SettingsWindow(_configProvider, _monitorsProvider, _metadataProvider);
            settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
            settingsWindow.ShowDialog();
        });
    }

    /// <summary>指定された金額を在庫から排出し、例外が発生した場合はダイアログを表示する。</summary>
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

    /// <summary>リソースと購読を解放する。</summary>
    public void Dispose()
    {
        _disposables.Dispose();
        _statusAggregator.Dispose();
    }
}

/// <summary>各金種の表示と操作を管理する ViewModel。</summary>
public class DenominationViewModel
{
    private readonly Inventory _inventory;
    /// <summary>この ViewModel が表す金種キー。</summary>
    public DenominationKey Key { get; }
    /// <summary>表示用の金種名。</summary>
    public string Name { get; }
    private readonly ReactiveProperty<int> _count;
    /// <summary>この金種の現在枚数。</summary>
    public ReadOnlyReactiveProperty<int> Count { get; }
    
    /// <summary>枚数を1枚増やすコマンド。</summary>
    public ReactiveCommand AddCommand { get; }
    /// <summary>枚数を1枚減らすコマンド。</summary>
    public ReactiveCommand RemoveCommand { get; }

    /// <summary>金種キー、在庫、メタデータプロバイダー、表示名を元にインスタンスを初期化する。</summary>
    public DenominationViewModel(Inventory inventory, DenominationKey key, Services.CurrencyMetadataProvider metadataProvider, string? displayName = null)
    {
        _inventory = inventory;
        Key = key;
        Name = !string.IsNullOrEmpty(displayName) ? displayName : metadataProvider.GetDenominationName(key);
        _count = new ReactiveProperty<int>(_inventory.GetCount(key));
        Count = _count;
        _inventory.Changed
            .Where(k => k == key)
            .Subscribe(_ =>
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _count.Value = _inventory.GetCount(key));
                }
                else
                {
                    _count.Value = _inventory.GetCount(key);
                }
            });
            
        AddCommand = new ReactiveCommand();
        AddCommand.Subscribe(_ => _inventory.Add(Key, 1));
        
        RemoveCommand = new ReactiveCommand();
        RemoveCommand.Subscribe(_ => _inventory.Add(Key, -1));
    }
}
