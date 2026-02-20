using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;
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
    public ReactiveProperty<bool> IsOverlapped { get; }
    /// <summary>一括投入画面が表示されているかどうか。</summary>
    public BindableReactiveProperty<bool> IsBulkInsertVisible { get; }
    /// <summary>クイック入金用の金額入力値。</summary>
    public BindableReactiveProperty<string> QuickDepositAmountInput { get; }

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
    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand<Unit> SimulateOverlapCommand { get; }

    // Bulk Deposit
    /// <summary>一括投入用のアイテムリスト。</summary>
    public ObservableCollection<BulkInsertItemViewModel> BulkInsertItems { get; } = [];
    /// <summary>一括投入画面を表示するコマンド。</summary>
    public ReactiveCommand<Unit> ShowBulkInsertCommand { get; }
    /// <summary>一括投入を実行するコマンド。</summary>
    public ReactiveCommand<Unit> InsertBulkCommand { get; }
    /// <summary>一括投入をキャンセルするコマンド。</summary>
    public ReactiveCommand<Unit> CancelBulkInsertCommand { get; }
    /// <summary>クイック入金を実行するコマンド。</summary>
    public ReactiveCommand<Unit> QuickDepositCommand { get; }

    public DepositViewModel(
        DepositController depositController,
        HardwareStatusManager hardwareStatusManager,
        Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        _depositController = depositController;
        _hardwareStatusManager = hardwareStatusManager;
        _logger = LogProvider.CreateLogger<DepositViewModel>();

        IsOverlapped = _hardwareStatusManager.IsOverlapped;
        IsBulkInsertVisible = new BindableReactiveProperty<bool>(false).AddTo(_disposables);
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

        // Commands
        BeginDepositCommand = IsInDepositMode.Select(x => !x).ToReactiveCommand<Unit>().AddTo(_disposables);
        BeginDepositCommand.Subscribe(_ =>
        {
            try
            {
                _depositController.BeginDeposit();
                PrepareBulkInsertItems(getDenominations());
                IsBulkInsertVisible.Value = false;
            }
            catch (Exception ex)
            {
                _logger.ZLogError(ex, $"Failed to begin deposit.");
                // Swallowed: error is logged, but we don't want to crash the UI thread
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

        CancelDepositCommand = IsInDepositMode.ToReactiveCommand<Unit>().AddTo(_disposables);
        CancelDepositCommand.Subscribe(_ =>
        {
            if (!_depositController.IsFixed) _depositController.FixDeposit();
            _depositController.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode.CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        ShowBulkInsertCommand.Subscribe(_ =>
        {
            PrepareBulkInsertItems(getDenominations());
            IsBulkInsertVisible.Value = !IsBulkInsertVisible.Value;
        });

        InsertBulkCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        InsertBulkCommand.Subscribe(_ =>
        {
            ExecuteBulkInsert();
            IsBulkInsertVisible.Value = false;
        });

        CancelBulkInsertCommand = new ReactiveCommand<Unit>().AddTo(_disposables);
        CancelBulkInsertCommand.Subscribe(_ =>
        {
            IsBulkInsertVisible.Value = false;
        });

        QuickDepositCommand = IsInDepositMode
            .CombineLatest(QuickDepositAmountInput, (mode, input) => !mode && decimal.TryParse(input, out var a) && a > 0)
            .ToReactiveCommand<Unit>().AddTo(_disposables);
        QuickDepositCommand.Subscribe(async _ => await ExecuteQuickDepositAsync(getDenominations()));

        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => mode && !fixed_ && !overlapped)
            .ToReactiveCommand<Unit>()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _hardwareStatusManager.SetOverlapped(true));
    }

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

    private void PrepareBulkInsertItems(IEnumerable<DenominationViewModel> denominations)
    {
        BulkInsertItems.Clear();
        foreach (var den in denominations)
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

    internal async Task ExecuteQuickDepositAsync(IEnumerable<DenominationViewModel> denominations)
    {
        if (!decimal.TryParse(QuickDepositAmountInput.Value, out var targetAmount)) return;

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
        // Consider a small delay to let UI react if desired, but for Quick Deposit it can be fast.
        await Task.Delay(100);
        _depositController.FixDeposit();
        await Task.Delay(100);
        _depositController.EndDeposit(CashDepositAction.NoChange);

        QuickDepositAmountInput.Value = "";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
