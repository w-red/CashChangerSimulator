using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
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
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DepositController _depositController;
    private readonly CompositeDisposable _disposables = new();

    /// <summary>ジャムが発生しているかどうかを制御する ReactiveProperty。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; }

    /// <summary>画面に表示する金種別情報のリスト。</summary>
    public ObservableCollection<DenominationViewModel> Denominations { get; } = [];
    private readonly ReactiveProperty<decimal> _totalAmount;
    /// <summary>在庫の合計金額（ReactiveProperty 版）。</summary>
    public ReadOnlyReactiveProperty<decimal> TotalAmount { get; }
    /// <summary>在庫の合計金額（WPF バインディング用の プレーンプロパティ）。</summary>
    public decimal TotalAmountCurrency => _totalAmount.Value;
    /// <summary>全金種のステータスを統合したデバイス全体のステータス (空・ニアエンプティ)。</summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>全金種のステータスを統合したデバイス全体のステータス (満杯・ニアフル)。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }
    /// <summary>最近の取引履歴のリスト。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];
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

    // Deposit Mode Properties
    public ReadOnlyReactiveProperty<bool> IsInDepositMode { get; }
    public ReadOnlyReactiveProperty<decimal> CurrentDepositAmount { get; }
    public ReadOnlyReactiveProperty<bool> IsDepositFixed { get; }
    public ReadOnlyReactiveProperty<bool> IsDepositPaused { get; }
    public ReadOnlyReactiveProperty<CashDepositStatus> DepositStatus { get; }
    public ReadOnlyReactiveProperty<string> CurrentModeName { get; }

    // WPF plain properties for binding
    public bool IsInDepositModePlain => IsInDepositMode.CurrentValue;
    public decimal CurrentDepositAmountPlain => CurrentDepositAmount.CurrentValue;
    public bool IsDepositFixedPlain => IsDepositFixed.CurrentValue;
    public bool IsDepositPausedPlain => IsDepositPaused.CurrentValue;
    public string CurrentModeNamePlain => CurrentModeName.CurrentValue;
    public CashStatus OverallStatusPlain => OverallStatus.CurrentValue;
    public CashStatus FullStatusPlain => FullStatus.CurrentValue;
    public bool IsBulkInsertDialogOpenPlain
    {
        get => IsBulkInsertDialogOpen.Value;
        set => IsBulkInsertDialogOpen.Value = value;
    }
    public bool IsBulkDispenseDialogOpenPlain
    {
        get => IsBulkDispenseDialogOpen.Value;
        set => IsBulkDispenseDialogOpen.Value = value;
    }

    // Deposit Mode Commands
    public ReactiveCommand BeginDepositCommand { get; }
    public ReactiveCommand PauseDepositCommand { get; }
    public ReactiveCommand ResumeDepositCommand { get; }
    public ReactiveCommand FixDepositCommand { get; }
    public ReactiveCommand StoreDepositCommand { get; }
    public ReactiveCommand CancelDepositCommand { get; }

    // Bulk Deposit
    public ObservableCollection<BulkInsertItemViewModel> BulkInsertItems { get; } = [];
    public ReactiveProperty<bool> IsBulkInsertDialogOpen { get; } = new(false);
    public ReactiveCommand ShowBulkInsertCommand { get; }
    public ReactiveCommand InsertBulkCommand { get; }
    public ReactiveCommand CancelBulkInsertCommand { get; }
    
    // Bulk Dispense
    public ObservableCollection<BulkInsertItemViewModel> BulkDispenseItems { get; } = [];
    public ReactiveProperty<bool> IsBulkDispenseDialogOpen { get; } = new(false);
    public ReactiveCommand ShowBulkDispenseCommand { get; }
    public ReactiveCommand DispenseBulkCommand { get; }
    public ReactiveCommand CancelBulkDispenseCommand { get; }

    private readonly Dictionary<string, List<string>> _errors = [];

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
        Services.CurrencyMetadataProvider metadataProvider,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController)
    {
        _inventory = inventory;
        _history = history;
        _manager = manager;
        _statusAggregator = aggregatorProvider.Aggregator;
        _configProvider = configProvider;
        _monitorsProvider = monitorsProvider;
        _metadataProvider = metadataProvider;
        _hardwareStatusManager = hardwareStatusManager;
        _depositController = depositController;

        IsJammed = _hardwareStatusManager.IsJammed;
        
        // Initialize ALL properties before any subscriptions
        IsInDepositMode = _depositController.Changed
            .Select(_ => _depositController.IsDepositInProgress)
            .ToReadOnlyReactiveProperty(_depositController.IsDepositInProgress)
            .AddTo(_disposables);

        CurrentDepositAmount = _depositController.Changed
            .Select(_ => _depositController.DepositAmount)
            .ToReadOnlyReactiveProperty(_depositController.DepositAmount)
            .AddTo(_disposables);

        IsDepositFixed = _depositController.Changed
            .Select(_ => _depositController.IsFixed)
            .ToReadOnlyReactiveProperty(_depositController.IsFixed)
            .AddTo(_disposables);

        DepositStatus = _depositController.Changed
            .Select(_ => _depositController.DepositStatus)
            .ToReadOnlyReactiveProperty(_depositController.DepositStatus)
            .AddTo(_disposables);

        IsDepositPaused = _depositController.Changed
            .Select(_ => _depositController.IsPaused)
            .ToReadOnlyReactiveProperty(_depositController.IsPaused)
            .AddTo(_disposables);

        CurrentModeName = _depositController.Changed
            .Select(_ => GetModeName())
            .ToReadOnlyReactiveProperty(GetModeName())
            .AddTo(_disposables);

        OverallStatus = _statusAggregator.DeviceStatus;
        FullStatus = _statusAggregator.FullStatus;

        // Plain property subscriptions (Safe now as all properties are initialized above)
        IsInDepositMode.Subscribe(_ => OnPropertyChanged(nameof(IsInDepositModePlain))).AddTo(_disposables);
        CurrentDepositAmount.Subscribe(_ => OnPropertyChanged(nameof(CurrentDepositAmountPlain))).AddTo(_disposables);
        IsDepositFixed.Subscribe(_ => OnPropertyChanged(nameof(IsDepositFixedPlain))).AddTo(_disposables);
        IsDepositPaused.Subscribe(_ => OnPropertyChanged(nameof(IsDepositPausedPlain))).AddTo(_disposables);
        CurrentModeName.Subscribe(_ => OnPropertyChanged(nameof(CurrentModeNamePlain))).AddTo(_disposables);
        OverallStatus.Subscribe(_ => OnPropertyChanged(nameof(OverallStatusPlain))).AddTo(_disposables);
        FullStatus.Subscribe(_ => OnPropertyChanged(nameof(FullStatusPlain))).AddTo(_disposables);
        IsBulkInsertDialogOpen.Subscribe(_ => OnPropertyChanged(nameof(IsBulkInsertDialogOpenPlain))).AddTo(_disposables);
        IsBulkDispenseDialogOpen.Subscribe(_ => OnPropertyChanged(nameof(IsBulkDispenseDialogOpenPlain))).AddTo(_disposables);

        // Deposit Commands
        BeginDepositCommand = IsInDepositMode.Select(x => !x).ToReactiveCommand().AddTo(_disposables);
        BeginDepositCommand.Subscribe(_ => _depositController.BeginDeposit());

        PauseDepositCommand = IsInDepositMode.CombineLatest(IsDepositPaused, IsDepositFixed, (mode, paused, fixed_) => mode && !paused && !fixed_)
            .ToReactiveCommand().AddTo(_disposables);
        PauseDepositCommand.Subscribe(_ => _depositController.PauseDeposit(CashDepositPause.Pause));

        ResumeDepositCommand = IsDepositPaused.ToReactiveCommand().AddTo(_disposables);
        ResumeDepositCommand.Subscribe(_ => _depositController.PauseDeposit(CashDepositPause.Restart));

        FixDepositCommand = IsInDepositMode.CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand().AddTo(_disposables);
        FixDepositCommand.Subscribe(_ => _depositController.FixDeposit());

        StoreDepositCommand = IsDepositFixed.ToReactiveCommand().AddTo(_disposables);
        StoreDepositCommand.Subscribe(_ => _depositController.EndDeposit(CashDepositAction.NoChange));

        CancelDepositCommand = IsInDepositMode.ToReactiveCommand().AddTo(_disposables);
        CancelDepositCommand.Subscribe(_ => 
        {
            // EndDeposit requires Fix state in currently implementation of DepositController,
            // but we might want to cancel directly. Let's ensure consistency.
            if (!_depositController.IsFixed) _depositController.FixDeposit();
            _depositController.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode.CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand().AddTo(_disposables);
        ShowBulkInsertCommand.Subscribe(_ => 
        {
            PrepareBulkInsertItems();
            IsBulkInsertDialogOpen.Value = true;
        });

        InsertBulkCommand = new ReactiveCommand().AddTo(_disposables);
        InsertBulkCommand.Subscribe(_ => 
        {
            ExecuteBulkInsert();
            IsBulkInsertDialogOpen.Value = false;
        });

        CancelBulkInsertCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkInsertCommand.Subscribe(_ => IsBulkInsertDialogOpen.Value = false);

        // Initialize Denominations
        foreach (var monitor in monitorsProvider.Monitors)
        {
            var key = monitor.Key;
            var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
            string? displayName = null;
            var config = _configProvider.Config;
            if (config.Inventory.TryGetValue(config.CurrencyCode, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                displayName = setting.DisplayName;
            }

            Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, _depositController, displayName));
        }

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

        // Auto-save inventory changes (throttle to avoid excessive I/O)
        _inventory.Changed
            .ThrottleLast(TimeSpan.FromSeconds(1))
            .Subscribe(_ =>
            {
                var state = new InventoryState { Counts = _inventory.ToDictionary() };
                ConfigurationLoader.SaveInventoryState(state);
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

        // Bulk Dispense Commands
        var canShowBulkDispense = IsInDepositMode
            .CombineLatest(IsJammed, (inDeposit, jammed) => !inDeposit && !jammed);

        ShowBulkDispenseCommand = canShowBulkDispense.ToReactiveCommand().AddTo(_disposables);
        ShowBulkDispenseCommand.Subscribe(_ =>
        {
            PrepareBulkDispenseItems();
            IsBulkDispenseDialogOpen.Value = true;
        });

        DispenseBulkCommand = new ReactiveCommand().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(_ =>
        {
            ExecuteBulkDispense();
            IsBulkDispenseDialogOpen.Value = false;
        });

        CancelBulkDispenseCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkDispenseCommand.Subscribe(_ => IsBulkDispenseDialogOpen.Value = false);

        // Settings Command
        OpenSettingsCommand = new RelayCommand(() =>
        {
            var settingsWindow = new SettingsWindow(_configProvider, _monitorsProvider, _metadataProvider)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
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

    private void PrepareBulkDispenseItems()
    {
        BulkDispenseItems.Clear();
        foreach (var den in Denominations)
        {
            // For dispense, we default to 0.
            BulkDispenseItems.Add(new BulkInsertItemViewModel(den.Key, den.Name));
        }
    }

    private void ExecuteBulkDispense()
    {
        var counts = BulkDispenseItems
            .Where(x => x.Quantity.Value > 0)
            .ToDictionary(x => x.Key, x => x.Quantity.Value);

        if (counts.Count > 0)
        {
            try
            {
                _manager.Dispense(counts);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Dispense Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private void PrepareBulkInsertItems()
    {
        BulkInsertItems.Clear();
        foreach (var den in Denominations)
        {
            BulkInsertItems.Add(new BulkInsertItemViewModel(den.Key, den.Name));
        }
    }

    private void ExecuteBulkInsert()
    {
        var counts = BulkInsertItems
            .Where(x => x.Quantity.Value > 0)
            .ToDictionary(x => x.Key, x => x.Quantity.Value);

        if (counts.Count > 0)
        {
            _depositController.TrackBulkDeposit(counts);
        }
    }

    private string GetModeName()
    {
        if (!_depositController.IsDepositInProgress && _depositController.DepositStatus != CashDepositStatus.End)
        {
            return "IDLE (待機中)";
        }

        if (_depositController.IsPaused)
        {
            return "PAUSED (一時停止中)";
        }

        if (_depositController.IsFixed)
        {
            return "DEPOSIT FIXED (確定済み)";
        }

        return _depositController.DepositStatus switch
        {
            CashDepositStatus.Start => "STARTING (開始中)",
            CashDepositStatus.Count => "COUNTING (計数中)",
            CashDepositStatus.End => "IDLE (待機中)",
            _ => "UNKNOWN"
        };
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
    
    // Commands removed as per UI simplification (Bulk Insert/Dispense only)

    /// <summary>入金を現在受け付けているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsAcceptingCash { get; }

    // WPF plain properties
    public int CountPlain => Count.CurrentValue;
    public bool IsAcceptingCashPlain => IsAcceptingCash.CurrentValue;

    /// <summary>金種キー、在庫、メタデータプロバイダー、表示名を元にインスタンスを初期化する。</summary>
    public DenominationViewModel(Inventory inventory, DenominationKey key, Services.CurrencyMetadataProvider metadataProvider, DepositController depositController, string? displayName = null)
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
            
        // 計数終了（Fix）または一時停止状態では投入を禁止する
        var canAdd = depositController.Changed
            .Select(_ => !depositController.IsFixed && !depositController.IsPaused)
            .ToReadOnlyReactiveProperty(!depositController.IsFixed && !depositController.IsPaused);

        IsAcceptingCash = depositController.Changed
            .Select(_ => depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused)
            .ToReadOnlyReactiveProperty(depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsFixed && !depositController.IsPaused);

        // Individual Add/Remove commands are removed to enforce Bulk Insert usage.

        // Subscribe for WPF updates
        Count.Skip(1).Subscribe(_ => System.Windows.Application.Current?.Dispatcher?.Invoke(() => INotifyPropertyChanged_OnPropertyChanged(nameof(CountPlain))));
        IsAcceptingCash.Skip(1).Subscribe(_ => System.Windows.Application.Current?.Dispatcher?.Invoke(() => INotifyPropertyChanged_OnPropertyChanged(nameof(IsAcceptingCashPlain))));
    }

    // Helper for INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void INotifyPropertyChanged_OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// バルク投入（一括投入）用の一時アイテム ViewModel。
/// </summary>
public class BulkInsertItemViewModel(DenominationKey key, string name)
{
    public DenominationKey Key { get; } = key;
    public string Name { get; } = name;
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
