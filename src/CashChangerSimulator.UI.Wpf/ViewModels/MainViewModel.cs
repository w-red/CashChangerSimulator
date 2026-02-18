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
public class MainViewModel : IDisposable
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
    private readonly CompositeDisposable _disposables = [];

    /// <summary>ジャムが発生しているかどうかを制御する ReactiveProperty。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; }

    /// <summary>紙幣などの重なりが発生しているかどうかを制御する ReactiveProperty。</summary>
    public BindableReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>画面に表示する金種別情報のリスト。</summary>
    public ObservableCollection<DenominationViewModel> Denominations { get; } = [];
    private readonly ReactiveProperty<decimal> _totalAmount;
    /// <summary>在庫の合計金額（ReactiveProperty 版）。</summary>
    public ReadOnlyReactiveProperty<decimal> TotalAmount { get; }
    /// <summary>全金種のステータスを統合したデバイス全体のステータス (空・ニアエンプティ)。</summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>全金種のステータスを統合したデバイス全体のステータス (満杯・ニアフル)。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }
    /// <summary>最近の取引履歴のリスト。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];
    /// <summary>設定画面を開くコマンド。</summary>
    public RelayCommand OpenSettingsCommand { get; }

    /// <summary>ユーザーが入力した払出金額（文字列）。</summary>
    public BindableReactiveProperty<string> DispenseAmountInput { get; }

    /// <summary>払出処理を実行するコマンド。</summary>
    public ReactiveCommand DispenseCommand { get; }

    // Deposit Mode Properties
    /// <summary>現在入金モード（入金セッション中）かどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsInDepositMode { get; }
    /// <summary>現在の入金セッションにおける合計投入金額。</summary>
    public ReadOnlyReactiveProperty<decimal> CurrentDepositAmount { get; }
    /// <summary>入金が確定（Fix）された状態かどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsDepositFixed { get; }
    /// <summary>入金処理が一時停止されているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsDepositPaused { get; }
    /// <summary>現在の入金ステータス（Start, Count, End など）。</summary>
    public ReadOnlyReactiveProperty<CashDepositStatus> DepositStatus { get; }
    /// <summary>現在のモード名（表示用）。</summary>
    public ReadOnlyReactiveProperty<string> CurrentModeName { get; }

    // WPF plain properties for binding (Removed)

    // Deposit Mode Commands
    /// <summary>新規入金を開始するコマンド。</summary>
    public ReactiveCommand BeginDepositCommand { get; }
    /// <summary>入金を一時停止するコマンド。</summary>
    public ReactiveCommand PauseDepositCommand { get; }
    /// <summary>入金を再開するコマンド。</summary>
    public ReactiveCommand ResumeDepositCommand { get; }
    /// <summary>入金を確定するコマンド。</summary>
    public ReactiveCommand FixDepositCommand { get; }
    /// <summary>入金を格納（預かり入れ）して終了するコマンド。</summary>
    public ReactiveCommand StoreDepositCommand { get; }
    /// <summary>入金をキャンセル（返却）して終了するコマンド。</summary>
    public ReactiveCommand CancelDepositCommand { get; }
    /// <summary>紙幣重なりエラーをシミュレーションするためのコマンド。</summary>
    public ReactiveCommand SimulateOverlapCommand { get; }

    // Bulk Deposit
    /// <summary>一括投入画面に表示するアイテムリスト。</summary>
    public ObservableCollection<BulkInsertItemViewModel> BulkInsertItems { get; } = [];
    /// <summary>一括投入画面を表示するコマンド。</summary>
    public ReactiveCommand ShowBulkInsertCommand { get; }
    /// <summary>一括投入を実行するコマンド。</summary>
    public ReactiveCommand InsertBulkCommand { get; }
    /// <summary>一括投入をキャンセルするコマンド。</summary>
    public ReactiveCommand CancelBulkInsertCommand { get; }
    
    // Bulk Dispense
    /// <summary>一括払出画面に表示するアイテムリスト。</summary>
    public ObservableCollection<BulkInsertItemViewModel> BulkDispenseItems { get; } = [];
    /// <summary>一括払出画面を表示するコマンド。</summary>
    public ReactiveCommand ShowBulkDispenseCommand { get; }
    /// <summary>一括払出を実行するコマンド。</summary>
    public ReactiveCommand DispenseBulkCommand { get; }
    /// <summary>一括払出をキャンセルするコマンド。</summary>
    public ReactiveCommand CancelBulkDispenseCommand { get; }

    /// <summary>各種プロバイダーを注入して MainViewModel を初期化する。</summary>
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
        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        
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
            var window = new BulkInsertWindow(this) { Owner = System.Windows.Application.Current.MainWindow };
            window.ShowDialog();
        });

        InsertBulkCommand = new ReactiveCommand().AddTo(_disposables);
        InsertBulkCommand.Subscribe(_ => ExecuteBulkInsert());

        CancelBulkInsertCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkInsertCommand.Subscribe(_ => { });

        // Overlap Simulation Command
        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => mode && !fixed_ && !overlapped)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _hardwareStatusManager.SetOverlapped(true));

        // Initialize Denominations
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

            Denominations.Add(new DenominationViewModel(_inventory, key, _metadataProvider, _depositController, monitor, displayName));
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
                    });
                }
                else
                {
                    _totalAmount.Value = total;
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
            
        // Dispense Logic
        DispenseAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text)
                    ? null
                    : !decimal.TryParse(text, out var val)
                    ? new Exception("Enter a valid number")
                    : val <= 0
                    ? new Exception("Amount must be positive")
                    : val > TotalAmount.CurrentValue
                    ? new Exception("Insufficient funds")
                    : null
            )
            .AddTo(_disposables);

        var hasDispenseErrors = Observable.FromEvent<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>(
                h => (s, e) => h(e),
                h => DispenseAmountInput.ErrorsChanged += h,
                h => DispenseAmountInput.ErrorsChanged -= h)
            .Select(_ => DispenseAmountInput.HasErrors)
            .Prepend(DispenseAmountInput.HasErrors);

        var canDispense = hasDispenseErrors
            .Select(x => !x)
            .CombineLatest(DispenseAmountInput, (noError, text) => noError && !string.IsNullOrEmpty(text));

        DispenseCommand = canDispense
            .ToReactiveCommand()
            .AddTo(_disposables);
            
        DispenseCommand.Subscribe(_ =>
        {
            if (decimal.TryParse(DispenseAmountInput.Value, out var amount))
            {
                DispenseCash(amount);
                DispenseAmountInput.Value = "";
            }
        });

        // Bulk Dispense Commands
        var canShowBulkDispense = IsInDepositMode
            .CombineLatest(IsJammed, (inDeposit, jammed) => !inDeposit && !jammed);

        ShowBulkDispenseCommand = canShowBulkDispense.ToReactiveCommand().AddTo(_disposables);
        ShowBulkDispenseCommand.Subscribe(_ =>
        {
            PrepareBulkDispenseItems();
            var window = new BulkDispenseWindow(this) { Owner = System.Windows.Application.Current.MainWindow };
            window.ShowDialog();
        });

        DispenseBulkCommand = new ReactiveCommand().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(_ => ExecuteBulkDispense());

        CancelBulkDispenseCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkDispenseCommand.Subscribe(_ => { });

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
        GC.SuppressFinalize(this);
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

    /// <summary>一括払出を実行し、結果をハンドリングする。</summary>
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

    /// <summary>一括投入用のアイテムリストを初期化する。</summary>
    private void PrepareBulkInsertItems()
    {
        BulkInsertItems.Clear();
        foreach (var den in Denominations)
        {
            BulkInsertItems.Add(new BulkInsertItemViewModel(den.Key, den.Name));
        }
    }

    /// <summary>一括投入を実行し、入金コントローラーに通知する。</summary>
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

    /// <summary>現在の入金状態に応じた表示用モード名を取得する。</summary>
    private string GetModeName()
    {
        return !_depositController.IsDepositInProgress && _depositController.DepositStatus != CashDepositStatus.End
            ? "IDLE (待機中)"
            : _depositController.IsPaused
                ? "PAUSED (一時停止中)"
                : _depositController.IsFixed
                    ? "DEPOSIT FIXED (確定済み)"
                    : _depositController.DepositStatus switch
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
    
    /// <summary>この金種の在庫状態（Full/NearFull/Normal/NearEmpty/Empty）。</summary>
    public ReadOnlyReactiveProperty<CashStatus> Status { get; }

    // Commands removed as per UI simplification (Bulk Insert/Dispense only)

    /// <summary>入金を現在受け付けているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsAcceptingCash { get; }

    /// <summary>金種キー、在庫、メタデータプロバイダー、表示名を元にインスタンスを初期化する。</summary>
    public DenominationViewModel(Inventory inventory, DenominationKey key, Services.CurrencyMetadataProvider metadataProvider, DepositController depositController, CashStatusMonitor monitor, string? displayName = null)
    {
        _inventory = inventory;
        Key = key;
        Name = !string.IsNullOrEmpty(displayName) ? displayName : metadataProvider.GetDenominationName(key);
        Status = monitor.Status;
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

}
}

/// <summary>バルク投入（一括投入）用の一時アイテム ViewModel。</summary>
public class BulkInsertItemViewModel(DenominationKey key, string name)
{
    public DenominationKey Key { get; } = key;
    public string Name { get; } = name;
    public ReactiveProperty<int> Quantity { get; } = new(0);
}
