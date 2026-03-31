using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.Extensions.Logging;
using R3;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>シミュレータのメインウィンドウに対応する ViewModel。</summary>
/// <remarks>
/// アプリケーション全体のライフサイクル管理、サブ ViewModel（Deposit, Dispense, Inventory, PosTransaction 等）の保持、
/// および設定に基づく初期動作（HotStart等）の制御を担当します。
/// </remarks>
public class MainViewModel : IDisposable
{
    private readonly CompositeDisposable _disposables = [];
    private readonly ConfigurationProvider _configProvider;
    private readonly IViewModelFactory _viewModelFactory;
    private readonly IDeviceFacade _facade;

    /// <summary>デバイスとコア機能の Facade。</summary>
    public IDeviceFacade Facade => _facade;

    /// <summary>設定プロバイダー。</summary>
    public ConfigurationProvider ConfigProvider => _configProvider;

    /// <summary>入金管理用の ViewModel。</summary>
    public DepositViewModel Deposit { get; }
    /// <summary>出金管理用の ViewModel。</summary>
    public DispenseViewModel Dispense { get; }
    /// <summary>在庫管理用の ViewModel。</summary>
    public InventoryViewModel Inventory { get; }
    /// <summary>高度なシミュレーション設定 ViewModel。</summary>
    public AdvancedSimulationViewModel AdvancedSimulation { get; }

    /// <summary>通貨記号の接頭辞。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    /// <summary>通貨記号の接尾辞。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>現在の UI 動作モード。</summary>
    public BindableReactiveProperty<UIMode> CurrentUIMode { get; }

    /// <summary>入金ウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenDepositCommand { get; }
    /// <summary>出金ウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenDispenseCommand { get; }
    /// <summary>高度なシミュレーションウィンドウを表示するコマンド。</summary>
    public ReactiveCommand OpenAdvancedSimulationCommand { get; }

    /// <summary>全体的な動作状態の表示名。</summary>
    public BindableReactiveProperty<string> GlobalModeName { get; }

    /// <summary>全依存関係を注入して MainViewModel を初期化し、サブ ViewModel を構築します。</summary>
    /// <param name="viewModelFactory">ViewModel 生成ファクトリ。</param>
    /// <param name="facade">デバイスとコア機能の Facade。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    /// <param name="notifyService">通知サービス。</param>
    /// <param name="scriptExecutionService">スクリプト実行サービス。</param>
    public MainViewModel(
        IViewModelFactory viewModelFactory,
        IDeviceFacade facade,
        ConfigurationProvider configProvider,
        CurrencyMetadataProvider metadataProvider,
        INotifyService notifyService,
        IScriptExecutionService scriptExecutionService)
    {
        ArgumentNullException.ThrowIfNull(viewModelFactory);
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        ArgumentNullException.ThrowIfNull(notifyService);
        ArgumentNullException.ThrowIfNull(scriptExecutionService);

        _viewModelFactory = viewModelFactory;
        _facade = facade;
        _configProvider = configProvider;
        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        var isDispenseBusy = new BindableReactiveProperty<bool>(false).AddTo(_disposables);

        // Ensure UI context for reactive updates. Always observe on the UI thread context
        // to prevent threading exceptions when sources fire from background threads.
        Observable<T> Sync<T>(Observable<T> observable)
        {
            var context = System.Threading.SynchronizationContext.Current 
                ?? (System.Windows.Application.Current?.Dispatcher != null 
                    ? new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Application.Current.Dispatcher) 
                    : null);

            return context != null ? observable.ObserveOn(context) : observable;
        }

        // Sub-ViewModels
        Inventory = _viewModelFactory.CreateInventoryViewModel(_configProvider).AddTo(_disposables);

        Deposit = _viewModelFactory.CreateDepositViewModel(
            () => Inventory.Denominations,
            isDispenseBusy)
            .AddTo(_disposables);

        Dispense = _viewModelFactory.CreateDispenseViewModel(
            Deposit.IsInDepositMode,
            () => Inventory.Denominations)
            .AddTo(_disposables);
            
        Dispense.IsBusy
            .Subscribe(busy => 
            {
                if (isDispenseBusy.IsDisposed) return;
                isDispenseBusy.Value = busy;
            })
            .AddTo(_disposables);

        AdvancedSimulation = _viewModelFactory.CreateAdvancedSimulationViewModel(_configProvider).AddTo(_disposables);

        CurrentUIMode = new BindableReactiveProperty<UIMode>(configProvider.Config.System.UIMode).AddTo(_disposables);

        Sync(configProvider.Reloaded)
            .Subscribe(_ => CurrentUIMode.Value = configProvider.Config.System.UIMode)
            .AddTo(_disposables);

        GlobalModeName = Sync(_facade.Status.IsConnected
            .CombineLatest(Deposit.CurrentModeName, Dispense.StatusName, (isConnected, depositMode, dispenseMode) =>
            {
                var idleStr = ResourceHelper.GetAsString("StatusIdle", "IDLE");
                var busyStr = ResourceHelper.GetAsString("StatusBusy", "Busy");
                
                return !isConnected
                    ? ResourceHelper.GetAsString("DeviceClosed", "CLOSED")
                    : (dispenseMode == busyStr || dispenseMode == "Busy")
                    ? ResourceHelper.GetAsString("Dispensing", "DISPENSING") : depositMode;
            }))
            .ToBindableReactiveProperty(_facade.Status.IsConnected.Value ? Deposit.CurrentModeName.Value : ResourceHelper.GetAsString("DeviceClosed", "CLOSED"))
            .AddTo(_disposables);

        OpenDepositCommand = Sync(_facade.Status.IsConnected).ToReactiveCommand().AddTo(_disposables);
        OpenDepositCommand.Subscribe(_ =>
        {
            _facade.View.ShowDepositWindow(Deposit, () => Inventory.Denominations);
        }).AddTo(_disposables);

        OpenDispenseCommand = Sync(_facade.Status.IsConnected).ToReactiveCommand().AddTo(_disposables);
        OpenDispenseCommand.Subscribe(_ =>
        {
            if (_facade.Status.IsJammed.Value || _facade.Status.IsOverlapped.Value)
            {
                _facade.Notify.ShowWarning(
                    ResourceHelper.GetAsString("ErrorCannotOpenTerminalInError", "Cannot open terminal while in error state."),
                    ResourceHelper.GetAsString("Warn", "Warning"));
                return;
            }

            _facade.View.ShowDispenseWindow(Dispense, () => Inventory.Denominations);
        }).AddTo(_disposables);

        OpenAdvancedSimulationCommand = Sync(_facade.Status.IsConnected).ToReactiveCommand().AddTo(_disposables);
        OpenAdvancedSimulationCommand.Subscribe(_ =>
        {
            _facade.View.ShowAdvancedSimulationWindow(AdvancedSimulation);
        }).AddTo(_disposables);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>非同期初期化処理（HotStart 等）を実行します。</summary>
    public async Task InitializeAsync()
    {
        // Auto-open device ONLY if HotStart is enabled.
        // We use the Inventory.OpenCommand to ensure logic is centralized in InventoryOperationService.
        if (_configProvider.Config.Simulation.HotStart)
        {
            try
            {
                if (Inventory.OpenCommand.CanExecute())
                {
                    Inventory.OpenCommand.Execute(Unit.Default);
                }
            }
            catch (Exception ex)
            {
                LogProvider.CreateLogger<MainViewModel>().LogError(ex, "Failed to auto-open device during Hot Start.");
            }
        }
    }
}
