using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金投入（入金）操作の UI 状態とロジックを管理する ViewModel。</summary>
/// <remarks>
/// <see cref="DepositController"/> と連携し、入金の開始・確定・キャンセル、および各種エラー状態のシミュレーション制御を担当します。
/// 金額のリアルタイム表示や、操作の有効・無効状態の制御も行います。
/// </remarks>
public class DepositViewModel : IDisposable
{
    private readonly ILogger<DepositViewModel> _logger = LogProvider.CreateLogger<DepositViewModel>();
    private readonly CompositeDisposable _disposables = [];
    private readonly IDeviceFacade _facade;
    private readonly Func<IEnumerable<DenominationViewModel>> _getDenominations;
    private readonly BindableReactiveProperty<bool> _isDispenseBusy;
    private readonly INotifyService _notifyService;
    private readonly CurrencyMetadataProvider _metadataProvider;
    private readonly IEnumerable<DenominationViewModel> _allDenominations;

    // --- State Properties ---
    
    /// <summary>エスクローにある金種のリスト（枚数 > 0 のもののみ）。</summary>
    public ReadOnlyReactiveProperty<IEnumerable<DenominationViewModel>> EscrowDenominations { get; }

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

    /// <summary>要求額。</summary>
    public BindableReactiveProperty<decimal> RequiredAmount { get; }

    /// <summary>不足額。</summary>
    public ReadOnlyReactiveProperty<decimal> RemainingAmount { get; }

    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; }
    private readonly BindableReactiveProperty<bool> _isOverlapped;

    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; }
    private readonly BindableReactiveProperty<bool> _isJammed;

    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }

    /// <summary>オーバーフロー金額。</summary>
    public BindableReactiveProperty<decimal> OverflowAmount { get; }

    /// <summary>オーバーフローが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> HasOverflow { get; }

    /// <summary>リジェクト金額。</summary>
    public BindableReactiveProperty<decimal> RejectAmount { get; }

    /// <summary>リジェクトが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> HasReject { get; }

    /// <summary>クイック入金用の金額入力値。</summary>
    public BindableReactiveProperty<string> QuickDepositAmountInput { get; }

    /// <summary>通貨記号。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }

    /// <summary>通貨単位。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    // --- Commands ---

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

    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }

    /// <summary>エラー状態を解消するコマンド。</summary>
    public ReactiveCommand<Unit> ResetErrorCommand { get; }

    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateJamCommand { get; }

    /// <summary>リジェクトをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateRejectCommand { get; }

    /// <summary>一括投入画面を表示するコマンド（View側で購読）。</summary>
    public ReactiveCommand<Unit> ShowBulkInsertCommand { get; }

    /// <summary>一括投入を実行するコマンド。</summary>
    public ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>> InsertBulkCommand { get; }

    /// <summary>クイック入金を実行するコマンド。</summary>
    public ReactiveCommand<Unit> QuickDepositCommand { get; }

    /// <summary>操作可能かどうか（エラーがなく、ビジーでない状態）。</summary>
    public BindableReactiveProperty<bool> CanOperate { get; }

    /// <summary>必要なサービスを注入して <see cref="DepositViewModel"/> を初期化します。</summary>
    /// <param name="facade">デバイスとコア機能の Facade である <see cref="IDeviceFacade"/>。</param>
    /// <param name="getDenominations">利用可能な金種 ViewModel のリストを取得する関数。</param>
    /// <param name="isDispenseBusy">出金処理中かどうかを示す反応型プロパティ。</param>
    /// <param name="notifyService">ユーザーへの通知（警告、エラー等）を行うサービス。</param>
    /// <param name="metadataProvider">通貨の表示形式（記号など）を提供するプロバイダー。</param>
    public DepositViewModel(
        IDeviceFacade facade,
        Func<IEnumerable<DenominationViewModel>> getDenominations,
        BindableReactiveProperty<bool> isDispenseBusy,
        INotifyService notifyService,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(isDispenseBusy);
        ArgumentNullException.ThrowIfNull(notifyService);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _facade = facade;
        _getDenominations = getDenominations;
        _isDispenseBusy = isDispenseBusy;
        _notifyService = notifyService;
        _metadataProvider = metadataProvider;
        _allDenominations = getDenominations().ToList();

        // UI Metadata
        CurrencyPrefix = _metadataProvider.SymbolPrefix.ToReadOnlyReactiveProperty().AddTo(_disposables);
        CurrencySuffix = _metadataProvider.SymbolSuffix.ToReadOnlyReactiveProperty().AddTo(_disposables);

        // Hardware State
        _isJammed = _facade.Status.IsJammed.ToBindableReactiveProperty().AddTo(_disposables);
        IsJammed = _isJammed.ToReadOnlyReactiveProperty().AddTo(_disposables);
        _isOverlapped = _facade.Status.IsOverlapped.ToBindableReactiveProperty().AddTo(_disposables);
        IsOverlapped = _isOverlapped.ToReadOnlyReactiveProperty().AddTo(_disposables);
        IsDeviceError = _facade.Status.IsDeviceError.ToBindableReactiveProperty().AddTo(_disposables);
        QuickDepositAmountInput = new BindableReactiveProperty<string>("").AddTo(_disposables);

        // Deposit State Observables
        IsInDepositMode = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.IsDepositInProgress)
            .ToBindableReactiveProperty(_facade.Deposit.IsDepositInProgress)
            .AddTo(_disposables);

        CurrentDepositAmount = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.DepositAmount)
            .ToBindableReactiveProperty(_facade.Deposit.DepositAmount)
            .AddTo(_disposables);

        IsDepositFixed = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.IsFixed)
            .ToBindableReactiveProperty(_facade.Deposit.IsFixed)
            .AddTo(_disposables);

        DepositStatus = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.DepositStatus)
            .ToBindableReactiveProperty(_facade.Deposit.DepositStatus)
            .AddTo(_disposables);

        IsDepositPaused = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.IsPaused)
            .ToBindableReactiveProperty(_facade.Deposit.IsPaused)
            .AddTo(_disposables);

        OverflowAmount = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.OverflowAmount)
            .ToBindableReactiveProperty(_facade.Deposit.OverflowAmount)
            .AddTo(_disposables);

        HasOverflow = OverflowAmount.Select(a => a > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);

        RejectAmount = _facade.Deposit.Changed
            .Select(_ => _facade.Deposit.RejectAmount)
            .ToBindableReactiveProperty(_facade.Deposit.RejectAmount)
            .AddTo(_disposables);

        HasReject = RejectAmount.Select(a => a > 0)
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);

        EscrowDenominations = _facade.Deposit.Changed
            .Select(_ => _allDenominations.Where(d => d.EscrowCount.Value > 0).ToList().AsEnumerable())
            .ToReadOnlyReactiveProperty(_allDenominations.Where(d => d.EscrowCount.Value > 0).ToList().AsEnumerable())
            .AddTo(_disposables);

        CurrentModeName = _facade.Deposit.Changed
            .Select(_ => GetModeName())
            .ToBindableReactiveProperty(GetModeName())
            .AddTo(_disposables);

        RequiredAmount = _facade.Deposit.Changed
            .Select(_ => _facade.Changer is SimulatorCashChanger scc ? scc.RequiredAmount : 0)
            .ToBindableReactiveProperty(_facade.Changer is SimulatorCashChanger s ? s.RequiredAmount : 0)
            .AddTo(_disposables);

        RemainingAmount = CurrentDepositAmount.CombineLatest(RequiredAmount, (current, required) => Math.Max(0, required - current))
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);

        // Commands Logic

        BeginDepositCommand = _facade.Status.IsConnected
            .CombineLatest(IsJammed, IsOverlapped, _isDispenseBusy, (connected, jammed, overlapped, dispenseBusyValue) => connected && !jammed && !overlapped && !dispenseBusyValue)
            .ToReactiveCommand<Unit>().AddTo(_disposables);

        BeginDepositCommand.Subscribe(_ =>
        {
            if (_isDispenseBusy.Value)
            {
                var msg = ResourceHelper.GetAsString("WarnDepositDuringDispense", "Cannot begin deposit while dispense is in progress.");
                var title = ResourceHelper.GetAsString("Warn", "Warning");
                _notifyService.ShowWarning(msg, title);
                return;
            }

            if (IsInDepositMode.Value) return;

            try
            {
                _facade.Deposit.BeginDeposit();
            }
            catch (PosControlException pcEx)
            {
                _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
                _logger.ZLogError(pcEx, $"Failed to begin deposit.");
                _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error"));
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Failed to begin deposit.");
                _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error"));
            }
        });

        PauseDepositCommand = IsInDepositMode
            .CombineLatest(IsDepositPaused, IsDepositFixed, (mode, paused, fixed_) => mode && !paused && !fixed_)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        PauseDepositCommand.Subscribe(_ => _facade.Deposit.PauseDeposit(CashDepositPause.Pause));

        ResumeDepositCommand = IsDepositPaused.ToReactiveCommand<Unit>().AddTo(_disposables);
        ResumeDepositCommand.Subscribe(_ => _facade.Deposit.PauseDeposit(CashDepositPause.Restart));

        FixDepositCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsJammed, IsOverlapped, (mode, fixed_, jammed, overlapped) => mode && !fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        FixDepositCommand.Subscribe(_ => _facade.Deposit.FixDeposit());

        StoreDepositCommand = IsDepositFixed
            .CombineLatest(IsJammed, IsOverlapped, (fixed_, jammed, overlapped) => fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        StoreDepositCommand.Subscribe(_ => _facade.Deposit.EndDeposit(CashDepositAction.NoChange));

        CancelDepositCommand = IsInDepositMode
            .CombineLatest(IsJammed, IsOverlapped, (mode, jammed, overlapped) => mode && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        CancelDepositCommand.Subscribe(_ =>
        {
            if (!_facade.Deposit.IsFixed) _facade.Deposit.FixDeposit();
            _facade.Deposit.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, _isJammed, _isOverlapped, (mode, fixed_, jammed, overlapped) => mode && !fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);

        InsertBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        InsertBulkCommand.Subscribe(counts =>
        {
            if (counts is { Count: > 0 })
            {
                _facade.Deposit.TrackBulkDeposit(counts);
            }
        });

        CanOperate = _facade.Status.IsConnected
            .CombineLatest(_isJammed, _isOverlapped, IsInDepositMode, _isDispenseBusy, (connected, jammed, overlapped, mode, dispenseBusyValue) => connected && !jammed && !overlapped && !mode && !dispenseBusyValue)
            .ToBindableReactiveProperty(_facade.Status.IsConnected.Value && !_isJammed.Value && !_isOverlapped.Value && !IsInDepositMode.Value && !_isDispenseBusy.Value)
            .AddTo(_disposables);

        QuickDepositCommand = CanOperate.ToReactiveCommand<Unit>().AddTo(_disposables);
        QuickDepositCommand.Subscribe(async _ =>
        {
            if (_isDispenseBusy.Value)
            {
                _notifyService.ShowWarning(
                    ResourceHelper.GetAsString("WarnDepositDuringDispense"),
                    ResourceHelper.GetAsString("Warn"));
                return;
            }

            if (IsInDepositMode.Value) return;

            if (!decimal.TryParse(QuickDepositAmountInput.Value, out var a) || a <= 0) return;

            await ExecuteQuickDepositAsync(_getDenominations());
        });

        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => !overlapped)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _facade.Status.SetOverlapped(true));

        ResetErrorCommand = IsOverlapped
            .CombineLatest(_facade.Status.IsJammed, (overlapped, jammed) => overlapped || jammed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _facade.Status.ResetError());

        SimulateJamCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsJammed, (mode, fixed_, jammed) => !jammed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _facade.Status.SetJammed(true));

        SimulateRejectCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateRejectCommand.Subscribe(_ => _facade.Deposit.SimulateReject(1000m));
    }

    private string GetModeName()
    {
        return !_facade.Deposit.IsDepositInProgress && _facade.Deposit.DepositStatus != CashDepositStatus.End
            ? "IDLE"
            : _facade.Deposit.IsPaused
                ? "PAUSED"
                : _facade.Deposit.IsFixed
                    ? "FIXED"
                    : _facade.Deposit.DepositStatus switch
                    {
                        CashDepositStatus.Start => "STARTING",
                        CashDepositStatus.Count => "COUNTING",
                        CashDepositStatus.End => "IDLE",
                        _ => "UNKNOWN"
                    };
    }

    /// <summary>
    /// クイック入金（金額直接入力によるシミュレーション）を非同期的に実行します。
    /// </summary>
    /// <param name="denominations">投入可能な金種の ViewModel リスト。</param>
    /// <returns>非同期タスク。</returns>
    internal async Task ExecuteQuickDepositAsync(IEnumerable<DenominationViewModel> denominations)
    {
        if (_isJammed.Value || _isOverlapped.Value)
        {
            _notifyService.ShowWarning(
                ResourceHelper.GetAsString("ErrorCannotOpenTerminalInError"),
                ResourceHelper.GetAsString("Warn"));
            return;
        }

        if (!decimal.TryParse(QuickDepositAmountInput.Value, out var targetAmount)) return;

        try
        {
            _facade.Deposit.BeginDeposit();
            var breakdown = new Dictionary<DenominationKey, int>();
            var remaining = targetAmount;
            var sortedDens = denominations.OrderByDescending(d => d.Key.Value);

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

            if (breakdown.Count > 0)
            {
                _facade.Deposit.TrackBulkDeposit(breakdown);
            }

            await Task.Delay(100);
            _facade.Deposit.FixDeposit();
            await Task.Delay(100);
            _facade.Deposit.EndDeposit(CashDepositAction.NoChange);

            QuickDepositAmountInput.Value = "";
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to execute quick deposit.");
            _notifyService.ShowWarning(pcEx.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to execute quick deposit.");
            _notifyService.ShowWarning(ex.Message, ResourceHelper.GetAsString("Error", "Error"));
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
