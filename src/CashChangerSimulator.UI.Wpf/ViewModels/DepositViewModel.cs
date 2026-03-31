using CashChangerSimulator.Core;
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
    private readonly IDepositOperationService _depositService;
    private readonly IInventoryOperationService _inventoryService;
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

    /// <summary>現在のエラーコード。</summary>
    public ReadOnlyReactiveProperty<int?> CurrentErrorCode { get; }

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

    /// <summary>要求額の入力値。</summary>
    public BindableReactiveProperty<string> RequiredAmountInput { get; }

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

    /// <summary>確定した入金を収納（釣銭無）するコマンド。</summary>
    public ReactiveCommand<Unit> StoreDepositNoChangeCommand { get; }

    /// <summary>確定した入金を収納（釣銭有）するコマンド。</summary>
    public ReactiveCommand<Unit> StoreDepositWithChangeCommand { get; }

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

    /// <summary>デバイスエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateDeviceErrorCommand { get; }

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
        IDepositOperationService depositService,
        IInventoryOperationService inventoryService,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(isDispenseBusy);
        ArgumentNullException.ThrowIfNull(depositService);
        ArgumentNullException.ThrowIfNull(inventoryService);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _facade = facade;
        _getDenominations = getDenominations;
        _isDispenseBusy = isDispenseBusy;
        _depositService = depositService;
        _inventoryService = inventoryService;
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
        CurrentErrorCode = _facade.Status.CurrentErrorCode.ToReadOnlyReactiveProperty().AddTo(_disposables);
        QuickDepositAmountInput = new BindableReactiveProperty<string>("").AddTo(_disposables);
        RequiredAmountInput = new BindableReactiveProperty<string>("").AddTo(_disposables);

        // Deposit State Observables
        IsInDepositMode = new BindableReactiveProperty<bool>(_facade.Deposit.IsDepositInProgress).AddTo(_disposables);
        _facade.Deposit.Changed
            .Subscribe(_ => IsInDepositMode.Value = _facade.Deposit.IsDepositInProgress)
            .AddTo(_disposables);

        CurrentDepositAmount = new BindableReactiveProperty<decimal>(_facade.Deposit.DepositAmount).AddTo(_disposables);
        IsDepositFixed = new BindableReactiveProperty<bool>(_facade.Deposit.IsFixed).AddTo(_disposables);
        DepositStatus = new BindableReactiveProperty<CashDepositStatus>(_facade.Deposit.DepositStatus).AddTo(_disposables);
        IsDepositPaused = new BindableReactiveProperty<bool>(_facade.Deposit.IsPaused).AddTo(_disposables);
        OverflowAmount = new BindableReactiveProperty<decimal>(_facade.Deposit.OverflowAmount).AddTo(_disposables);

        _facade.Deposit.Changed
            .Subscribe(_ =>
            {
                CurrentDepositAmount.Value = _facade.Deposit.DepositAmount;
                IsDepositFixed.Value = _facade.Deposit.IsFixed;
                DepositStatus.Value = _facade.Deposit.DepositStatus;
                IsDepositPaused.Value = _facade.Deposit.IsPaused;
                OverflowAmount.Value = _facade.Deposit.OverflowAmount;
            })
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

        // Sync RequiredAmount (from device) to RequiredAmountInput
        RequiredAmount.Subscribe(val =>
        {
            if (!decimal.TryParse(RequiredAmountInput.Value, out var current) || current != val)
            {
                RequiredAmountInput.Value = val > 0 ? val.ToString("G29") : "";
            }
        }).AddTo(_disposables);

        // Sync RequiredAmountInput to RequiredAmount (to device)
        RequiredAmountInput.Subscribe(input =>
        {
            if (_facade.Changer is not SimulatorCashChanger scc) return;

            if (decimal.TryParse(input, out var val) && val >= 0)
            {
                if (scc.RequiredAmount != val)
                {
                    scc.RequiredAmount = val;
                    // Trigger manual change notification if needed, 
                    // though SimulatorCashChanger should fire events when RequiredAmount is set.
                }
            }
            else if (string.IsNullOrWhiteSpace(input))
            {
                if (scc.RequiredAmount != 0) scc.RequiredAmount = 0;
            }
        }).AddTo(_disposables);

        RemainingAmount = CurrentDepositAmount.CombineLatest(RequiredAmount, (current, required) => Math.Max(0, required - current))
            .ToReadOnlyReactiveProperty()
            .AddTo(_disposables);

        // Commands Logic

        BeginDepositCommand = _facade.Status.IsConnected
            .CombineLatest(IsJammed, IsOverlapped, _isDispenseBusy, (connected, jammed, overlapped, dispenseBusyValue) => connected && !jammed && !overlapped && !dispenseBusyValue)
            .ToReactiveCommand<Unit>().AddTo(_disposables);

        BeginDepositCommand.Subscribe(_ =>
        {
            if (IsInDepositMode.Value) return;
            _depositService.BeginDeposit();
        });

        PauseDepositCommand = IsInDepositMode
            .CombineLatest(IsDepositPaused, IsDepositFixed, (mode, paused, fixed_) => mode && !paused && !fixed_)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        PauseDepositCommand.Subscribe(_ => _depositService.PauseDeposit(CashDepositPause.Pause));

        ResumeDepositCommand = IsDepositPaused.ToReactiveCommand<Unit>().AddTo(_disposables);
        ResumeDepositCommand.Subscribe(_ => _depositService.PauseDeposit(CashDepositPause.Restart));

        FixDepositCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsJammed, IsOverlapped, (mode, fixed_, jammed, overlapped) => mode && !fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        FixDepositCommand.Subscribe(_ => _depositService.FixDeposit());

        StoreDepositNoChangeCommand = IsDepositFixed
            .CombineLatest(IsJammed, IsOverlapped, (fixed_, jammed, overlapped) => fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        StoreDepositNoChangeCommand.Subscribe(_ => _depositService.EndDeposit(CashDepositAction.NoChange));

        StoreDepositWithChangeCommand = IsDepositFixed
            .CombineLatest(IsJammed, IsOverlapped, RemainingAmount, (fixed_, jammed, overlapped, remaining) => fixed_ && !jammed && !overlapped && remaining == 0)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        StoreDepositWithChangeCommand.Subscribe(_ => _depositService.EndDeposit(CashDepositAction.Change));

        CancelDepositCommand = IsInDepositMode
            .CombineLatest(IsJammed, IsOverlapped, (mode, jammed, overlapped) => mode && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        CancelDepositCommand.Subscribe(_ =>
        {
            if (!_facade.Deposit.IsFixed) _depositService.FixDeposit();
            _depositService.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, _isJammed, _isOverlapped, (mode, fixed_, jammed, overlapped) => mode && !fixed_ && !jammed && !overlapped)
            .ToReactiveCommand<Unit>().AddTo(_disposables);

        InsertBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        InsertBulkCommand.Subscribe(counts =>
        {
            if (counts is { Count: > 0 })
            {
                _depositService.TrackBulkDeposit(counts);
            }
        });

        CanOperate = _facade.Status.IsConnected
            .CombineLatest(_isJammed, _isOverlapped, IsDeviceError, IsInDepositMode, _isDispenseBusy, 
                (connected, jammed, overlapped, devErr, mode, dispenseBusyValue) => connected && !jammed && !overlapped && !devErr && !mode && !dispenseBusyValue)
            .ToBindableReactiveProperty(_facade.Status.IsConnected.Value && !_isJammed.Value && !_isOverlapped.Value && !IsDeviceError.Value && !IsInDepositMode.Value && !_isDispenseBusy.Value)
            .AddTo(_disposables);

        QuickDepositCommand = CanOperate.ToReactiveCommand<Unit>().AddTo(_disposables);
        QuickDepositCommand.Subscribe(async _ =>
        {
            if (IsInDepositMode.Value) return;

            if (!decimal.TryParse(QuickDepositAmountInput.Value, out var a) || a <= 0) return;

            await ExecuteQuickDepositAsync(_getDenominations());
        });

        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => !overlapped)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _inventoryService.SimulateOverlap());

        ResetErrorCommand = IsOverlapped
            .CombineLatest(_facade.Status.IsJammed, IsDeviceError, (overlapped, jammed, devErr) => overlapped || jammed || devErr)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        ResetErrorCommand.Subscribe(_ => _inventoryService.ResetError());

        SimulateJamCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsJammed, (mode, fixed_, jammed) => !jammed)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _inventoryService.SimulateJam());

        SimulateRejectCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateRejectCommand.Subscribe(_ => _depositService.SimulateReject(1000m));

        SimulateDeviceErrorCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsDeviceError, (mode, fixed_, devErr) => !devErr)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateDeviceErrorCommand.Subscribe(_ => _inventoryService.SimulateDeviceError());
    }

    private string GetModeName()
    {
        return !_facade.Deposit.IsDepositInProgress && _facade.Deposit.DepositStatus != CashDepositStatus.End
            ? ResourceHelper.GetAsString("StatusIdle", "IDLE")
            : _facade.Deposit.IsPaused
                ? ResourceHelper.GetAsString("StatusPaused", "PAUSED")
                : _facade.Deposit.IsFixed
                    ? ResourceHelper.GetAsString("StatusFixed", "FIXED")
                    : _facade.Deposit.DepositStatus switch
                    {
                        CashDepositStatus.Start => ResourceHelper.GetAsString("StatusStarting", "STARTING"),
                        CashDepositStatus.Count => ResourceHelper.GetAsString("StatusCounting", "COUNTING"),
                        CashDepositStatus.End => ResourceHelper.GetAsString("StatusIdle", "IDLE"),
                        _ => ResourceHelper.GetAsString("StatusUnknown", "UNKNOWN")
                    };
    }

    /// <summary>
    /// クイック入金（金額直接入力によるシミュレーション）を非同期的に実行します。
    /// </summary>
    /// <param name="denominations">投入可能な金種の ViewModel リスト。</param>
    /// <returns>非同期タスク。</returns>
    internal async Task ExecuteQuickDepositAsync(IEnumerable<DenominationViewModel> denominations)
    {
        if (!decimal.TryParse(QuickDepositAmountInput.Value, out var targetAmount)) return;

        await _depositService.ExecuteQuickDepositAsync(targetAmount, denominations.Select(d => d.Key));
        QuickDepositAmountInput.Value = "";
    }

    /// <summary>
    /// ウィンドウが閉じられた際のクリーンアップ処理を行います。
    /// </summary>
    public void HandleWindowClosed()
    {
        if (IsInDepositMode.Value)
        {
            _logger.ZLogInformation($"Window closed during deposit. Ending session as NoChange.");
            if (!IsDepositFixed.Value)
            {
                _depositService.FixDeposit();
            }
            _depositService.EndDeposit(CashDepositAction.NoChange);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
