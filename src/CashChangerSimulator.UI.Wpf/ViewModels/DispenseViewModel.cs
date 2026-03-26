using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.Services;
using R3;

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
    private readonly IDispenseOperationService _dispenseService;
    private readonly IInventoryOperationService _inventoryService;
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

    /// <summary>デバイスエラーをシミュレートするコマンド。</summary>
    public ReactiveCommand SimulateDeviceErrorCommand { get; }

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
        IDispenseOperationService dispenseService,
        IInventoryOperationService inventoryService,
        CurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(facade);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(isInDepositMode);
        ArgumentNullException.ThrowIfNull(getDenominations);
        ArgumentNullException.ThrowIfNull(dispenseService);
        ArgumentNullException.ThrowIfNull(inventoryService);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _facade = facade;
        _configProvider = configProvider;
        _isInDepositMode = isInDepositMode;
        _dispenseService = dispenseService;
        _inventoryService = inventoryService;

        CurrencyPrefix = metadataProvider.SymbolPrefix;
        CurrencySuffix = metadataProvider.SymbolSuffix;
        IsDeviceError = _facade.Status.IsDeviceError;

        // --- State Mapping ---

        Status = new BindableReactiveProperty<CashDispenseStatus>(_facade.Dispense.Status).AddTo(_disposables);
        StatusName = new BindableReactiveProperty<string>(ResourceHelper.GetAsString("Status" + _facade.Dispense.Status.ToString(), _facade.Dispense.Status.ToString())).AddTo(_disposables);

        _facade.Dispense.Changed
            .Subscribe(_ =>
            {
                Status.Value = _facade.Dispense.Status;
                StatusName.Value = ResourceHelper.GetAsString("Status" + _facade.Dispense.Status.ToString(), _facade.Dispense.Status.ToString());
            })
            .AddTo(_disposables);

        IsBusy = Status
            .Select(s => s == CashDispenseStatus.Busy)
            .ToBindableReactiveProperty(_facade.Dispense.Status == CashDispenseStatus.Busy)
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
            if (decimal.TryParse(DispenseAmountInput.Value, out var amount))
            {
                DispensingAmount.Value = amount;
                _dispenseService.DispenseCash(amount);
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
            DispensingAmount.Value = d.Key.Value;
            _dispenseService.ExecuteBulkDispense(new Dictionary<DenominationKey, int> { [d.Key] = 1 });
        });

        DispenseBulkCommand = new ReactiveCommand<IReadOnlyDictionary<DenominationKey, int>>().AddTo(_disposables);
        DispenseBulkCommand.Subscribe(counts =>
        {
            if (counts is { Count: > 0 })
            {
                DispensingAmount.Value = counts.Sum(kvp => kvp.Key.Value * kvp.Value);
                _dispenseService.ExecuteBulkDispense(counts);
            }
        });

        ResetErrorCommand = Status
            .Select(s => s == CashDispenseStatus.Error)
            .CombineLatest(_facade.Status.IsJammed, _facade.Status.IsOverlapped, IsDeviceError, (err, jammed, overlapped, devErr) => err || jammed || overlapped || devErr)
            .ToReactiveCommand()
            .AddTo(_disposables);

        ResetErrorCommand.Subscribe(_ => _inventoryService.ResetError());

        SimulateJamCommand = IsJammed
            .Select(jammed => !jammed)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateJamCommand.Subscribe(_ => _inventoryService.SimulateJam());

        SimulateOverlapCommand = IsOverlapped
            .Select(o => !o)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateOverlapCommand.Subscribe(_ => _inventoryService.SimulateOverlap());

        SimulateDeviceErrorCommand = IsDeviceError
            .Select(err => !err)
            .ToReactiveCommand()
            .AddTo(_disposables);
        SimulateDeviceErrorCommand.Subscribe(_ => _inventoryService.SimulateDeviceError());
    }

    // Private helper methods removed as logic is now in IDispenseOperationService

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
