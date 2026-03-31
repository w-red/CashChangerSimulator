using CashChangerSimulator.Core;
using Microsoft.Extensions.Logging;
using ZLogger;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.UI.Wpf.Services;
using R3;
using System.Collections.ObjectModel;
using System.Windows;
using System.Threading;
using System.Windows.Threading;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金在庫の可視化とデバイスの基本操作（接続・エラー解除）を担当する ViewModel。</summary>
public class InventoryViewModel : IDisposable
{
    private readonly ILogger<InventoryViewModel>? _logger = LogProvider.CreateLogger<InventoryViewModel>();
    private readonly IDeviceFacade _facade;
    private readonly ConfigurationProvider _configProvider;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly IInventoryOperationService _operationService;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly IDispatcherService _dispatcher;
    private readonly CompositeDisposable _disposables = [];

    /// <summary>通貨の接頭辞（例: ￥）。</summary>
    public BindableReactiveProperty<string> CurrencyPrefix { get; } = default!;
    /// <summary>通貨の接尾辞。</summary>
    public BindableReactiveProperty<string> CurrencySuffix { get; } = default!;

    /// <summary>紙幣金種のリスト。</summary>
    public ObservableCollection<DenominationViewModel> BillDenominations { get; } = [];
    /// <summary>硬貨金種のリスト。</summary>
    public ObservableCollection<DenominationViewModel> CoinDenominations { get; } = [];
    /// <summary>全金種の列挙。</summary>
    public IEnumerable<DenominationViewModel> Denominations => BillDenominations.Concat(CoinDenominations);

    /// <summary>デバイス全体の在庫ステータス。</summary>
    public BindableReactiveProperty<CashStatus> OverallStatus { get; }
    /// <summary>フル状態のステータス。</summary>
    public BindableReactiveProperty<CashStatus> FullStatus { get; }
    /// <summary>ジャムが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsJammed { get; }
    /// <summary>重なりが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsOverlapped { get; }
    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }
    /// <summary>現在のエラーコード。</summary>
    public BindableReactiveProperty<int?> CurrentErrorCode { get; }
    /// <summary>デバイスと接続されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsConnected { get; }

    /// <summary>取引履歴が空かどうか。</summary>
    public BindableReactiveProperty<bool> IsEmpty { get; }
    /// <summary>UI上の紙幣エリアの幅割合。</summary>
    public BindableReactiveProperty<GridLength> BillGridWidth { get; }
    /// <summary>UI上の硬貨エリアの幅割合。</summary>
    public BindableReactiveProperty<GridLength> CoinGridWidth { get; }

    /// <summary>デバイスをオープンするコマンド。</summary>
    public ReactiveCommand<Unit> OpenCommand { get; }
    /// <summary>デバイスをクローズするコマンド。</summary>
    public ReactiveCommand<Unit> CloseCommand { get; }
    /// <summary>設定画面を表示するコマンド。</summary>
    public ReactiveCommand<Unit> OpenSettingsCommand { get; }
    /// <summary>エラーをリセットするコマンド。</summary>
    public ReactiveCommand<Unit> ResetErrorCommand { get; }
    /// <summary>全在庫を回収するコマンド。</summary>
    public ReactiveCommand<Unit> CollectAllCommand { get; }
    /// <summary>全在庫を補充するコマンド。</summary>
    public ReactiveCommand<Unit> ReplenishAllCommand { get; }
    /// <summary>金種詳細を表示するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> ShowDenominationDetailCommand { get; }
    /// <summary>取引履歴をエクスポートするコマンド。</summary>
    public ReactiveCommand<Unit> ExportHistoryCommand { get; }
    /// <summary>リカバリヘルプを表示するコマンド。</summary>
    public ReactiveCommand<Unit> ShowRecoveryHelpCommand { get; }

    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateJamCommand { get; }
    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }
    /// <summary>デバイスエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateDeviceErrorCommand { get; }

    /// <summary>リカバリヘルプが状況的に推奨されるかどうか。</summary>
    public BindableReactiveProperty<bool> IsRecoveryHelpAvailable { get; }

    /// <summary>最近の取引履歴。</summary>
    public ObservableCollection<TransactionEntry> RecentTransactions { get; } = [];

    /// <summary>依存関係を注入して InventoryViewModel を初期化します。</summary>
    public InventoryViewModel(
        IDeviceFacade facade,
        ConfigurationProvider configProvider,
        CurrencyMetadataProvider metadataProvider,
        IInventoryOperationService operationService,
        IViewModelFactory viewModelFactory,
        IDispatcherService dispatcher)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(operationService);
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _facade = facade;
        _configProvider = configProvider;
        _metadataProvider = metadataProvider;
        _operationService = operationService;
        _viewModelFactory = viewModelFactory;
        _dispatcher = dispatcher;

        // Ensure UI context for reactive updates. Always observe on the UI thread context
        // to prevent threading exceptions when sources fire from background threads.
        Observable<T> Sync<T>(Observable<T> observable)
        {
            return observable.ObserveOn(new CashChangerSimulator.UI.Wpf.Services.DispatcherServiceSynchronizationContext(_dispatcher));
        }

        CurrencyPrefix = _metadataProvider.SymbolPrefix.ToBindableReactiveProperty("").AddTo(_disposables);
        CurrencySuffix = _metadataProvider.SymbolSuffix.ToBindableReactiveProperty("").AddTo(_disposables);

        OverallStatus = Sync(_facade.AggregatorProvider.Aggregator.DeviceStatus.AsObservable())
            .ToBindableReactiveProperty(_facade.AggregatorProvider.Aggregator.DeviceStatus.CurrentValue)
            .AddTo(_disposables);

        FullStatus = Sync(_facade.AggregatorProvider.Aggregator.FullStatus.AsObservable())
            .ToBindableReactiveProperty(_facade.AggregatorProvider.Aggregator.FullStatus.CurrentValue)
            .AddTo(_disposables);

        IsJammed = Sync(_facade.Status.IsJammed.AsObservable())
            .ToBindableReactiveProperty(_facade.Status.IsJammed.Value)
            .AddTo(_disposables);

        IsOverlapped = Sync(_facade.Status.IsOverlapped.AsObservable())
            .ToBindableReactiveProperty(_facade.Status.IsOverlapped.Value)
            .AddTo(_disposables);

        IsDeviceError = Sync(_facade.Status.IsDeviceError.AsObservable())
            .ToBindableReactiveProperty(_facade.Status.IsDeviceError.Value)
            .AddTo(_disposables);

        CurrentErrorCode = Sync(_facade.Status.CurrentErrorCode.AsObservable())
            .ToBindableReactiveProperty(_facade.Status.CurrentErrorCode.Value)
            .AddTo(_disposables);

        IsConnected = Sync(_facade.Status.IsConnected.AsObservable())
            .ToBindableReactiveProperty(_facade.Status.IsConnected.Value)
            .AddTo(_disposables);

        BillGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        CoinGridWidth = new BindableReactiveProperty<GridLength>(new GridLength(1, GridUnitType.Star)).AddTo(_disposables);
        IsEmpty = new BindableReactiveProperty<bool>(RecentTransactions.Count == 0).AddTo(_disposables);

        OpenSettingsCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        OpenSettingsCommand.Subscribe(_ =>
        {
            _facade.View.ShowSettingsWindow();
        });

        ResetErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _operationService.ResetError());

        ExportHistoryCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ExportHistoryCommand.Subscribe(_ => _operationService.ExportHistory());

        SimulateJamCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _operationService.SimulateJam());

        SimulateOverlapCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _operationService.SimulateOverlap());

        SimulateDeviceErrorCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        SimulateDeviceErrorCommand.Subscribe(_ => _operationService.SimulateDeviceError());

        IsRecoveryHelpAvailable = Observable.CombineLatest(
            IsJammed,
            IsOverlapped,
            OverallStatus,
            FullStatus,
            (jammed, overlapped, status, full) => jammed || overlapped || (status != CashStatus.Normal && status != CashStatus.Unknown) || full != CashStatus.Normal
        ).ToBindableReactiveProperty().AddTo(_disposables);

        ShowRecoveryHelpCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ShowRecoveryHelpCommand.Subscribe(_ =>
        {
            _facade.View.ShowRecoveryHelpDialogAsync(this);
        });

        Sync(_facade.Monitors.Changed)
            .Subscribe(_ => SafeInvoke(InitializeDenominations))
            .AddTo(_disposables);

        Sync(_facade.History.Added)
            .Subscribe(entry => SafeInvoke(() =>
            {
                RecentTransactions.Insert(0, entry);
                if (RecentTransactions.Count > 50) RecentTransactions.RemoveAt(50);
                IsEmpty.Value = RecentTransactions.Count == 0;
            }))
            .AddTo(_disposables);

        // Field initializers execute before constructor, but let's be super safe.
        BillDenominations ??= [];
        CoinDenominations ??= [];

        SafeInvoke(InitializeDenominations);

        OpenCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        OpenCommand.Subscribe(_ => _operationService.OpenDevice());
 
        CloseCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        CloseCommand.Subscribe(_ => _operationService.CloseDevice());

        CollectAllCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        CollectAllCommand.Subscribe(_ => _operationService.CollectAll());

        ReplenishAllCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        ReplenishAllCommand.Subscribe(_ => _operationService.ReplenishAll());

        ShowDenominationDetailCommand = new ReactiveCommand<DenominationViewModel>().AddTo(_disposables);
        ShowDenominationDetailCommand.Subscribe(vm =>
        {
            if (vm == null) return;
            _facade.View.ShowDenominationDetailDialogAsync(vm);
        });
    }

    private void SafeInvoke(Action action)
    {
        _dispatcher.SafeInvoke(action);
    }

    /// <summary>金種の初期化とグリッド幅の更新を行います。</summary>
    public void InitializeDenominations()
    {
        if (BillDenominations == null || CoinDenominations == null) return;

        foreach (var vm in BillDenominations) vm?.Dispose();
        foreach (var vm in CoinDenominations) vm?.Dispose();
        BillDenominations.Clear();
        CoinDenominations.Clear();

        if (_facade.Monitors?.Monitors == null) return;

        _logger?.ZLogDebug($"InitializeDenominations: Found {_facade.Monitors.Monitors.Count()} monitors.");
        foreach (var monitor in _facade.Monitors.Monitors)
        {
            var key = monitor.Key;
            var setting = _configProvider.Config.GetDenominationSetting(key);

            if (setting.IsRecyclable || setting.IsDepositable)
            {
                var vm = _viewModelFactory.CreateDenominationViewModel(key);
                if (vm != null)
                {
                    vm.ShowDetailCommand.Subscribe(x => ShowDenominationDetailCommand.Execute(x)).AddTo(_disposables);
                    if (key.Type == CurrencyCashType.Bill)
                    {
                        BillDenominations.Add(vm);
                    }
                    else
                    {
                        CoinDenominations.Add(vm);
                    }
                }
            }
        }
        UpdateGridRatios();
    }

    private void UpdateGridRatios()
    {
        if (BillDenominations == null || CoinDenominations == null) return;

        int billCount = BillDenominations.Count;
        int coinCount = CoinDenominations.Count;

        if (billCount == 0 && coinCount == 0)
        {
            BillGridWidth.Value = new GridLength(1, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(1, GridUnitType.Star);
        }
        else if (billCount == 0)
        {
            BillGridWidth.Value = new GridLength(0, GridUnitType.Pixel);
            CoinGridWidth.Value = new GridLength(1, GridUnitType.Star);
        }
        else if (coinCount == 0)
        {
            BillGridWidth.Value = new GridLength(1, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(0, GridUnitType.Pixel);
        }
        else
        {
            BillGridWidth.Value = new GridLength(billCount, GridUnitType.Star);
            CoinGridWidth.Value = new GridLength(coinCount, GridUnitType.Star);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
