using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.Services;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using R3;
using ZLogger;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>現金払い出し（出金）操作の UI 状態とロジックを管理する ViewModel。</summary>
/// <remarks>
/// `DispenseController` および `CashChangerManager` と連携し、指定金額や金種構成による払い出しを実行します。
/// 在庫合計の監視や、払い出しコマンドのバリデーション（在庫不足チェック等）を担当します。
/// </remarks>
public class DispenseViewModel : IDisposable
{
    private readonly IDeviceFacade _facade;
    private readonly ConfigurationProvider _configProvider;
    private readonly ILogger<DispenseViewModel> _logger;
    private readonly INotifyService _notifyService;
    private readonly BindableReactiveProperty<bool> _isInDepositMode;
    private readonly CompositeDisposable _disposables = [];

    // --- State Properties ---

    /// <summary>現在の在庫合計金額。</summary>
    public BindableReactiveProperty<decimal> TotalAmount { get; }

    /// <summary>出金金額の入力値。</summary>
    public BindableReactiveProperty<string> DispenseAmountInput { get; }

    /// <summary>通貨記号。</summary>
    public ReadOnlyReactiveProperty<string> CurrencyPrefix { get; }

    /// <summary>通貨単位。</summary>
    public ReadOnlyReactiveProperty<string> CurrencySuffix { get; }

    /// <summary>出金ステータス。</summary>
    public BindableReactiveProperty<CashDispenseStatus> Status { get; }

    /// <summary>出金ステータスの表示名。</summary>
    public BindableReactiveProperty<string> StatusName { get; }

    /// <summary>出金処理中かどうか。</summary>
    public BindableReactiveProperty<bool> IsBusy { get; }

    /// <summary>現在出金中の合計金額。</summary>
    public BindableReactiveProperty<decimal> DispensingAmount { get; }

    /// <summary>利用可能な金種リスト。</summary>
    public IEnumerable<DenominationViewModel> Denominations { get; }

    /// <summary>ジャムが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsJammed { get; }

    /// <summary>重なりエラーが発生しているかどうか。</summary>
    public ReadOnlyReactiveProperty<bool> IsOverlapped { get; }

    /// <summary>操作可能かどうか（エラーがなく、ビジーでない状態）。</summary>
    public BindableReactiveProperty<bool> CanOperate { get; }

    /// <summary>デバイスエラーが発生しているかどうか。</summary>
    public BindableReactiveProperty<bool> IsDeviceError { get; }

    // --- Commands ---

    /// <summary>出金を実行するコマンド。</summary>
    public ReactiveCommand DispenseCommand { get; }

    /// <summary>一括出金画面を表示するコマンド（View側で購読）。</summary>
    public ReactiveCommand ShowBulkDispenseCommand { get; }

    /// <summary>一括出金を実行するコマンド。</summary>
    public ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>> DispenseBulkCommand { get; }

    /// <summary>特定の金種を1枚出金するコマンド。</summary>
    public ReactiveCommand<DenominationViewModel> QuickDispenseCommand { get; }

    /// <summary>エラー状態を解消するコマンド。</summary>
    public ReactiveCommand ResetErrorCommand { get; }

    /// <summary>ジャムエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand SimulateJamCommand { get; }

    /// <summary>重なりエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand SimulateOverlapCommand { get; }

    /// <summary>必要なサービスを注入して <see cref="DispenseViewModel"/> を初期化します。</summary>
    /// <param name="facade">デバイスとコア機能の Facade である <see cref="IDeviceFacade"/>。</param>
    /// <param name="configProvider">アプリケーション設定を提供する <see cref="ConfigurationProvider"/>。</param>
    /// <param name="isInDepositMode">現在入金モード中かどうかを示す反応型プロパティ。</param>
    /// <param name="getDenominations">利用可能な金種 ViewModel のリストを取得する関数。</param>
    /// <param name="notifyService">ユーザーへの通知を行うサービス。</param>
    /// <param name="metadataProvider">通貨の表示形式（記号など）を提供するプロバイダー。</param>
    public DispenseViewModel(
        IDeviceFacade facade,
        ConfigurationProvider configProvider,
        BindableReactiveProperty<bool> isInDepositMode,
        Func<IEnumerable<DenominationViewModel>> getDenominations,
        INotifyService notifyService,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(isInDepositMode);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(notifyService);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _facade = facade;
        _configProvider = configProvider;
        _isInDepositMode = isInDepositMode;
        _notifyService = notifyService;
        _logger = LogProvider.CreateLogger<DispenseViewModel>();

        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        IsDeviceError = _facade.Status.IsDeviceError;

        // --- State Mapping ---

        Status = _facade.Dispense.Changed
            .Select(_ => _facade.Dispense.Status)
            .ToBindableReactiveProperty(_facade.Dispense.Status)
            .AddTo(_disposables);

        IsBusy = Status
            .Select(s => s == CashDispenseStatus.Busy)
            .ToBindableReactiveProperty(false)
            .AddTo(_disposables);

        StatusName = Status
            .Select(s => s.ToString())
            .ToBindableReactiveProperty("Idle")
            .AddTo(_disposables);

        IsJammed = _facade.Status.IsJammed.ToReadOnlyReactiveProperty().AddTo(_disposables);
        IsOverlapped = _facade.Status.IsOverlapped.ToReadOnlyReactiveProperty().AddTo(_disposables);

        CanOperate = IsBusy
            .CombineLatest(IsJammed, IsOverlapped, _isInDepositMode, (busy, jammed, overlapped, deposit) => !busy && !jammed && !overlapped && !deposit)
            .ToBindableReactiveProperty(!IsBusy.Value && !IsJammed.CurrentValue && !IsOverlapped.CurrentValue && !_isInDepositMode.Value)
            .AddTo(_disposables);

        DispensingAmount = new BindableReactiveProperty<decimal>(0m).AddTo(_disposables);

        TotalAmount = new BindableReactiveProperty<decimal>(_facade.Inventory.CalculateTotal(_configProvider.Config.System.CurrencyCode)).AddTo(_disposables);
        _facade.Inventory.Changed
            .Subscribe(_ =>
            {
                var total = _facade.Inventory.CalculateTotal(_configProvider.Config.System.CurrencyCode);
                _facade.Dispatcher.SafeInvoke(() => TotalAmount.Value = total);
            })
            .AddTo(_disposables);

        DispenseAmountInput = new BindableReactiveProperty<string>("")
            .EnableValidation(text =>
                string.IsNullOrWhiteSpace(text)
                    ? null
                    : !decimal.TryParse(text, out var val)
                    ? new Exception(ResourceHelper.GetAsString("ErrorEnterValidNumber", "Enter a valid number"))
                    : val <= 0
                    ? new Exception(ResourceHelper.GetAsString("ErrorAmountPositive", "Amount must be positive"))
                    : val > TotalAmount.Value
                    ? new Exception(ResourceHelper.GetAsString("ErrorInsufficientFunds", "Insufficient funds"))
                    : null
            )
            .AddTo(_disposables);

        // --- Commands Logic ---

        DispenseCommand = DispenseAmountInput
            .Select(_ => !DispenseAmountInput.HasErrors && !string.IsNullOrWhiteSpace(DispenseAmountInput.Value))
            .CombineLatest(IsBusy, IsJammed, IsOverlapped, _isInDepositMode, (can, busy, jammed, overlapped, deposit) => can && !busy && !jammed && !overlapped && !deposit)
            .ToReactiveCommand()
            .AddTo(_disposables);

        DispenseCommand.Subscribe(_ =>
        {
            if (_isInDepositMode.Value)
            {
                var msg = ResourceHelper.GetAsString("WarnDispenseDuringDeposit", "Cannot dispense while deposit is in progress.");
                var title = ResourceHelper.GetAsString("Warn", "Warning");
                _notifyService.ShowWarning(msg, title);
                return;
            }

            if (decimal.TryParse(DispenseAmountInput.Value, out var amount))
            {
                DispenseCash(amount);
                DispenseAmountInput.Value = "";
            }
        });

        var canDispense = IsBusy.Select(busy => !busy);

        ShowBulkDispenseCommand = canDispense
            .CombineLatest(IsJammed, IsOverlapped, _isInDepositMode, (can, jammed, overlapped, deposit) => can && !jammed && !overlapped && !deposit)
            .ToReactiveCommand()
            .AddTo(_disposables);

        Denominations = getDenominations().ToList();

        QuickDispenseCommand = IsBusy
            .CombineLatest(IsJammed, IsOverlapped, _isInDepositMode, (busy, jammed, overlapped, deposit) => !busy && !jammed && !overlapped && !deposit)
            .ToReactiveCommand<DenominationViewModel>().AddTo(_disposables);

        QuickDispenseCommand.Subscribe(d =>
        {
            if (d == null) return;

            if (_isInDepositMode.Value)
            {
                _notifyService.ShowWarning(
                    ResourceHelper.GetAsString("WarnDispenseDuringDeposit"),
                    ResourceHelper.GetAsString("Warn"));
                return;
            }

            ExecuteBulkDispense(new Dictionary<DenominationKey, int> { [d.Key] = 1 });
        });

        DispenseBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(counts =>
        {
            if (counts is { Count: > 0 })
            {
                ExecuteBulkDispense(counts);
            }
        });

        ResetErrorCommand = Status
            .Select(s => s == CashDispenseStatus.Error)
            .CombineLatest(_facade.Status.IsJammed, _facade.Status.IsOverlapped, (err, jammed, overlapped) => err || jammed || overlapped)
            .ToReactiveCommand()
            .AddTo(_disposables);

        ResetErrorCommand.Subscribe(_ => _facade.Status.ResetError());

        SimulateJamCommand = IsJammed
            .Select(jammed => !jammed)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _facade.Status.SetJammed(true));

        SimulateOverlapCommand = IsOverlapped
            .Select(o => !o)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _facade.Status.SetOverlapped(true));
    }

    private void DispenseCash(decimal amount)
    {
        try
        {
            DispensingAmount.Value = amount;
            _facade.Dispense.DispenseChangeAsync(amount, true, (code, ext) => { }, _configProvider.Config.System.CurrencyCode)
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        _logger.ZLogError(t.Exception, $"Background dispense (amount) failed potentially late.");
                    }
                });
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to dispense {amount}.");
            _notifyService.ShowError(pcEx.Message);
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense {amount}.");
            _notifyService.ShowError(ex.Message);
        }
    }

    private void ExecuteBulkDispense(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        try
        {
            var total = counts.Sum(x => x.Key.Value * x.Value);
            DispensingAmount.Value = total;
            _facade.Dispense.DispenseCashAsync(counts, true, (code, ext) => { })
                .ContinueWith(t => {
                    if (t.IsFaulted) {
                        _logger.ZLogError(t.Exception, $"Background dispense (bulk) failed potentially late.");
                    }
                });
        }
        catch (PosControlException pcEx)
        {
            _facade.Status.SetDeviceError((int)pcEx.ErrorCode, pcEx.ErrorCodeExtended);
            _logger.ZLogError(pcEx, $"Failed to dispense cash (bulk).");
            _notifyService.ShowError(pcEx.Message, "Dispense Error");
        }
        catch (Exception ex)
        {
            _logger.ZLogError(ex, $"Failed to dispense cash (bulk).");
            _notifyService.ShowError(ex.Message, "Dispense Error");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
