using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;
using System.Collections.ObjectModel;

namespace CashChangerSimulator.UI.Wpf.ViewModels;

/// <summary>
/// 入金コンポーネントを制御する ViewModel。
/// </summary>
public class DepositViewModel : IDisposable
{
    private readonly DepositController _depositController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly CompositeDisposable _disposables = [];

    // State Properties
    public BindableReactiveProperty<bool> IsInDepositMode { get; }
    public BindableReactiveProperty<decimal> CurrentDepositAmount { get; }
    public BindableReactiveProperty<bool> IsDepositFixed { get; }
    public BindableReactiveProperty<CashDepositStatus> DepositStatus { get; }
    public BindableReactiveProperty<bool> IsDepositPaused { get; }
    public BindableReactiveProperty<string> CurrentModeName { get; }
    public ReactiveProperty<bool> IsOverlapped { get; }

    // Commands
    public ReactiveCommand BeginDepositCommand { get; }
    public ReactiveCommand PauseDepositCommand { get; }
    public ReactiveCommand ResumeDepositCommand { get; }
    public ReactiveCommand FixDepositCommand { get; }
    public ReactiveCommand StoreDepositCommand { get; }
    public ReactiveCommand CancelDepositCommand { get; }
    public ReactiveCommand SimulateOverlapCommand { get; }

    // Bulk Deposit
    public ObservableCollection<BulkInsertItemViewModel> BulkInsertItems { get; } = [];
    public ReactiveCommand ShowBulkInsertCommand { get; }
    public ReactiveCommand InsertBulkCommand { get; }
    public ReactiveCommand CancelBulkInsertCommand { get; }


    public DepositViewModel(
        DepositController depositController,
        HardwareStatusManager hardwareStatusManager,
        Func<IEnumerable<DenominationViewModel>> getDenominations)
    {
        _depositController = depositController;
        _hardwareStatusManager = hardwareStatusManager;

        IsOverlapped = _hardwareStatusManager.IsOverlapped;

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
        BeginDepositCommand = IsInDepositMode.Select(x => !x).ToReactiveCommand().AddTo(_disposables);
        BeginDepositCommand.Subscribe(_ => 
        {
            try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", "BeginDepositCommand subscribed.\n"); } catch {}
            try
            {
                _depositController.BeginDeposit();
                try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", "Controller.BeginDeposit finished.\n"); } catch {}
                OpenBulkInsertWindow(getDenominations());
            }
            catch (Exception ex)
            {
                try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", $"Exception: {ex}\n"); } catch {}
            }
        });

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
            if (!_depositController.IsFixed) _depositController.FixDeposit();
            _depositController.EndDeposit(CashDepositAction.Repay);
        });

        ShowBulkInsertCommand = IsInDepositMode.CombineLatest(IsDepositFixed, (mode, fixed_) => mode && !fixed_)
            .ToReactiveCommand().AddTo(_disposables);
        ShowBulkInsertCommand.Subscribe(_ => OpenBulkInsertWindow(getDenominations()));

        InsertBulkCommand = new ReactiveCommand().AddTo(_disposables);
        InsertBulkCommand.Subscribe(_ => ExecuteBulkInsert());

        CancelBulkInsertCommand = new ReactiveCommand().AddTo(_disposables);
        CancelBulkInsertCommand.Subscribe(_ => { });

        SimulateOverlapCommand = IsInDepositMode
            .CombineLatest(IsDepositFixed, IsOverlapped, (mode, fixed_, overlapped) => mode && !fixed_ && !overlapped)
            .ToReactiveCommand()
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

    private void OpenBulkInsertWindow(IEnumerable<DenominationViewModel> denominations)
    {
        try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", "OpenBulkInsertWindow called.\n"); } catch {}
        PrepareBulkInsertItems(denominations);

        var mainWindow = System.Windows.Application.Current?.MainWindow;
        try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", $"MainWindow: {mainWindow}\n"); } catch {}
        
        if (mainWindow != null)
        {
            var window = new BulkInsertWindow(this) { Owner = mainWindow };
            window.Show();
            try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", "Window showed.\n"); } catch {}
        }
        else
        {
             try { System.IO.File.AppendAllText(@"C:\Users\ITI202301003_User\debug_deposit.txt", "MainWindow is null.\n"); } catch {}
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}
