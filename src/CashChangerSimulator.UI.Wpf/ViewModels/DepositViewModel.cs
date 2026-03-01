using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>入金コンポーネントを制御する ViewModel。</summary>
public class DepositViewModel : IDisposable
{
    private readonly DepositController _depositController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly ILogger<DepositViewModel> _logger;
    private readonly CompositeDisposable _disposables = [];

    // State Properties
    /// <summary>入金モード中かどうか。</summary>
    public BindableReactiveProperty<bool> IsInDepositMode { get; }
    /// <summary>現在の入金合計額。</summary>
    public BindableReactiveProperty<decimal> CurrentDepositAmount { get; }
    /// <summary>入金が確定されたかどうか。</summary>
    public BindableReactiveProperty<bool> IsDepositFixed { get; }
    /// <summary>入金ステータス。</summary>
    public BindableReactiveProperty<CashDepositStatus> DepositStatus { get; }
    /// <summary>入金が一時停止中かどうか。</summary>
    public BindableReactiveProperty<bool> IsDepositPaused { get; }
    /// <summary>現在の動作モードの表示名。</summary>
    public BindableReactiveProperty<string> CurrentModeName { get; }
    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; }
    private readonly BindableReactiveProperty<bool> _isOverlapped;
    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; }
    private readonly BindableReactiveProperty<bool> _isJammed;
    /// <summary>クイック入金用の金額入力値。</summary>
    public BindableReactiveProperty<string> QuickDepositAmountInput { get; }
    /// <summary>通貨記号。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    // Commands
    /// <summary>入金を開始するコマンド。</summary>
    public ReactiveCommand<Unit> BeginDepositCommand { get; }
    /// <summary>入金を一時停止するコマンド。</summary>
    public ReactiveCommand<Unit> PauseDepositCommand { get; }
    /// <summary>入金を再開するコマンド。</summary>
    public ReactiveCommand<Unit> ResumeDepositCommand { get; }
    /// <summary>入金を確定するコマンド。</summary>
    public ReactiveCommand<Unit> FixDepositCommand { get; }
    /// <summary>確定した入金を収納するコマンド。</summary>
    public ReactiveCommand<Unit> StoreDepositCommand { get; }
    /// <summary>入金をキャンセル（返却）するコマンド。</summary>
    public ReactiveCommand<Unit> CancelDepositCommand { get; }
    
    // Phase 12: Error Reset
    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }
    /// <summary>エラー状態を解消するコマンド。</summary>
    public ReactiveCommand<Unit> ResetErrorCommand { get; }
    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateJamCommand { get; }

    // Bulk Deposit
    /// <summary>一括投入画面を表示するコマンド（View側で購読）。</summary>
    public ReactiveCommand<Unit> ShowBulkInsertCommand { get; }
    /// <summary>一括投入を実行するコマンド。</summary>
    public ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>> InsertBulkCommand { get; }
    /// <summary>クイック入金を実行するコマンド。</summary>
    public ReactiveCommand<Unit> QuickDepositCommand { get; }
    /// <summary>操作可能かどうか（エラーがなく、ビジーでない状態）。</summary>
    public BindableReactiveProperty<bool> CanOperate { get; }

    private readonly BindableReactiveProperty<bool> _isDispenseBusy;
    private readonly INotifyService _notifyService;

    /// <summary>DepositViewModel の新しいインスタンスを初期化します。</summary>
    public DepositViewModel(
        DepositController depositController,
        HardwareStatusManager hardwareStatusManager,
        Func<IEnumerable<DenominationViewModel>> getDenominations,
        BindableReactiveProperty<bool> isDispenseBusy,
        INotifyService notifyService,
        CurrencyMetadataProvider metadataProvider)
    {
        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        _depositController = depositController;
        _hardwareStatusManager = hardwareStatusManager;
        _isDispenseBusy = isDispenseBusy;
        _notifyService = notifyService;
        _logger = LogProvider.CreateLogger<DepositViewModel>();
        
        _isJammed = _hardwareStatusManager.IsJammed.ToBindableReactiveProperty().AddTo(_disposables);
        IsJammed = _isJammed.ToReadOnlyReactiveProperty().AddTo(_disposables);
        _isOverlapped = _hardwareStatusManager.IsOverlapped.ToBindableReactiveProperty().AddTo(_disposables);
        IsOverlapped = _isOverlapped.ToReadOnlyReactiveProperty().AddTo(_disposables);
        QuickDepositAmountInput = new BindableReactiveProperty<string>("").AddTo(_disposables);

        IsInDepositMode = _depositController.Changed
            .Select(_ => _depositController.IsDepositInProgress)
            .ToBindableReactiveProperty(_depositController.IsDepositInProgress)
            .AddTo(_disposables);

        CurrentDepositAmount = _depositController.Changed
            .Select(_ => _depositController.DepositAmount)
            .ToBindableReactiveProperty(_depositController.DepositAmount)
            .AddTo(_disposables);

        IsDepositFixed = _depositController.Changed
            .Select(_ => _depositController.IsFixed)
            .ToBindableReactiveProperty(_depositController.IsFixed)
            .AddTo(_disposables);

        DepositStatus = _depositController.Changed
            .Select(_ => _depositController.DepositStatus)
            .ToBindableReactiveProperty(_depositController.DepositStatus)
            .AddTo(_disposables);

        IsDepositPaused = _depositController.Changed
            .Select(_ => _depositController.IsPaused)
            .ToBindableReactiveProperty(_depositController.IsPaused)
            .AddTo(_disposables);

        CurrentModeName = _depositController.Changed
            .Select(_ => GetModeName())
            .ToBindableReactiveProperty(GetModeName())
            .AddTo(_disposables);

        BeginDepositCommand = IsJammed.CombineLatest(IsOverlapped, _isDispenseBusy, (jammed, overlapped, dispenseBusy) => !jammed && !overlapped && !dispenseBusy)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        BeginDepositCommand.Subscribe(_ =>
        {
            if (_isDispenseBusy.Value)
            {
                var msg = System.Windows.Application.Current?.Resources["StrWarnDepositDuringDispense"] as string ?? "Cannot begin deposit while dispense is in progress.";
                var title = System.Windows.Application.Current?.Resources["StrWarn"] as string ?? "Warning";
                _notifyService.ShowWarning(msg, title);
                return;
            }

            if (IsInDepositMode.Value) return;

            try
            {
                _depositController.BeginDeposit();
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Failed to begin deposit.");
                _notifyService.ShowWarning(ex.Message, (string)System.Windows.Application.Current.Resources["StrError"]);
            }
        });

        PauseDepositCommand = IsInDepositMode.CombineLatest(IsDepositPaused, IsDepositFixed, (mode, paused, fixed_) => mode && !paused && !fixed_)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        PauseDepositCommand.Subscribe(_ => _depositController.PauseDeposit(CashDepositPause.Pause));

        ResumeDepositCommand = IsDepositPaused.ToReactiveCommand<Unit>().AddTo(_disposables);
        ResumeDepositCommand.Subscribe(_ => _depositController.PauseDeposit(CashDepositPause.Restart));

        FixDepositCommand = IsInDepositMode.CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        FixDepositCommand.Subscribe(_ => _depositController.FixDeposit());

        StoreDepositCommand = IsDepositFixed.ToReactiveCommand<Unit>().AddTo(_disposables);
        StoreDepositCommand.Subscribe(_ => _depositController.EndDeposit(CashDepositAction.NoChange));

        CancelDepositCommand = IsInDepositMode
            .CombineLatest(IsJammed, IsOverlapped, (mode, jammed, overlapped) => mode && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        CancelDepositCommand.Subscribe(_ =>
        {
            if (!_depositController.IsFixed) _depositController.FixDeposit();
            _depositController.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode.CombineLatest(IsDepositFixed, _isJammed, _isOverlapped, 
            (mode, fixed_, jammed, overlapped) => mode && !fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);

        InsertBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        InsertBulkCommand.Subscribe(counts =>
        {
            if (counts != null && counts.Count > 0)
            {
                _depositController.TrackBulkDeposit(counts);
            }
        });

        CanOperate = _isJammed.CombineLatest(_isOverlapped, IsInDepositMode, _isDispenseBusy, (jammed, overlapped, mode, dispenseBusy) => !jammed && !overlapped && !mode && !dispenseBusy)
            .ToBindableReactiveProperty(!_isJammed.Value && !_isOverlapped.Value && !IsInDepositMode.Value && !_isDispenseBusy.Value)
            .AddTo(_disposables);

        QuickDepositCommand = CanOperate
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        QuickDepositCommand.Subscribe(async _ =>
        {
            if (_isDispenseBusy.Value)
            {
                _notifyService.ShowWarning(
                    (string)System.Windows.Application.Current.Resources["StrWarnDepositDuringDispense"],
                    (string)System.Windows.Application.Current.Resources["StrWarn"]);
                return;
            }

            if (IsInDepositMode.Value) return;

            // Instead of evaluating in CanExecute, check validity here
            if (!decimal.TryParse(QuickDepositAmountInput.Value, out var a) || a <= 0) return;

            await ExecuteQuickDepositAsync(getDenominations());
        });

        // Phase 12: Error Reset
        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => !overlapped)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _hardwareStatusManager.SetOverlapped(true));

        ResetErrorCommand = IsOverlapped.CombineLatest(_hardwareStatusManager.IsJammed, (overlapped, jammed) => overlapped || jammed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _hardwareStatusManager.ResetError());

        SimulateJamCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsJammed, (mode, fixed_, jammed) => !jammed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _hardwareStatusManager.SetJammed(true));
    }

    private string GetModeName()
    {
        return !_depositController.IsDepositInProgress && _depositController.DepositStatus != CashDepositStatus.End
            ? "IDLE"
            : _depositController.IsPaused
                ? "PAUSED"
                : _depositController.IsFixed
                    ? "FIXED"
                    : _depositController.DepositStatus switch
                    {
                        CashDepositStatus.Start => "STARTING",
                        CashDepositStatus.Count => "COUNTING",
                        CashDepositStatus.End => "IDLE",
                        _ => "UNKNOWN"
                    };
    }

    internal async Task ExecuteQuickDepositAsync(IEnumerable<DenominationViewModel> denominations)
    {
        if (_isJammed.Value || _isOverlapped.Value)
        {
            _notifyService.ShowWarning(
                (string)System.Windows.Application.Current.Resources["StrErrorCannotOpenTerminalInError"],
                (string)System.Windows.Application.Current.Resources["StrWarn"]);
            return;
        }

        if (!decimal.TryParse(QuickDepositAmountInput.Value, out var targetAmount)) return;

        try
        {
            // Start Deposit
            _depositController.BeginDeposit();

            // Calculate greedy breakdown
            var breakdown = new Dictionary<DenominationKey, int>();
            var remaining = targetAmount;

            // Sort denominations descending (e.g., 10000, 5000...)
            var sortedDens = denominations
                .OrderByDescending(d => d.Key.Value);

            foreach (var den in sortedDens)
            {
                if (den.Key.Value <= 0) continue;
                int count = (int)(remaining / den.Key.Value);
                if (count > 0)
                {
                    breakdown[den.Key] = count;
                    remaining -= count * den.Key.Value;
                }
            }

            // Insert
            if (breakdown.Count > 0)
            {
                _depositController.TrackBulkDeposit(breakdown);
            }

            // Auto Fix & Store
            await Task.Delay(100);
            _depositController.FixDeposit();
            await Task.Delay(100);
            _depositController.EndDeposit(CashDepositAction.NoChange);

            QuickDepositAmountInput.Value = "";
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to execute quick deposit.");
            _notifyService.ShowWarning(ex.Message, (string)System.Windows.Application.Current.Resources["StrError"]);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
