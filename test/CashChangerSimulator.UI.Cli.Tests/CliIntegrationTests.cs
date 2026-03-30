using Moq;
using Shouldly;
using Spectre.Console.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Services;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;

using Spectre.Console;

namespace CashChangerSimulator.UI.Cli.Tests;

/// <summary>CLI コマンド経由でコアロジック（Device/Core）の動作を検証する統合テスト。</summary>
public class CliIntegrationTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestConsole _console;
    private readonly Mock<ILineReader> _mockReader;
    private readonly SimulatorCashChanger _changer;
    private readonly ICliCommandDispatcher _dispatcher;

    public CliIntegrationTests()
    {
        var services = new ServiceCollection();
        _console = new TestConsole();
        _mockReader = new Mock<ILineReader>();

        // CLI の DI 設定を流用
        CliDIContainer.ConfigureServices(services, Array.Empty<string>());
        
        // テスト用に一部のサービスを差し替え
        services.AddSingleton<IAnsiConsole>(_console);
        services.AddSingleton<ILineReader>(_mockReader.Object);
        
        // ローカライズのモック
        var mockLocalizer = new Mock<IStringLocalizer>();
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, $"[[{s}]]"));
        mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args != null && args.Length > 0 ? $"[[{s}]] {string.Join(" ", args)}" : $"[[{s}]]"));
        services.AddSingleton<IStringLocalizer>(mockLocalizer.Object);

        // 前もって CliSessionOptions を登録し、IsAsync を設定しておく
        var options = new CliSessionOptions();
        options.IsAsync = true;
        services.AddSingleton(options);

        _serviceProvider = services.BuildServiceProvider();
        _changer = _serviceProvider.GetRequiredService<SimulatorCashChanger>();
        _dispatcher = _serviceProvider.GetRequiredService<ICliCommandDispatcher>();

        // デバイスの初期化（Open/Claim/Enable）
        _changer.Open();
        _changer.Claim(1000);
        _changer.DeviceEnabled = true;

        // 在庫をクリア
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();
    }

    /// <summary>入金の開始から確定、終了までの一連のフローをテストし、在庫が増加することを確認します。</summary>
    [Fact]
    public async Task DepositCycleShouldIncreaseInventoryCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        var tenYen = new DenominationKey(10, CurrencyCashType.Coin, "JPY");
        inventory.GetCount(tenYen).ShouldBe(0);

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 100"); // 100円分
        
        // 入金中に計数をシミュレート（IsAsync=true の場合、自動的には増えないため）
        _changer.DepositController.TrackDeposit(new DenominationKey(100, CurrencyCashType.Bill, "JPY"));
        
        // 入金中の状態を確認 (IsAsync=true なので Count に留まる)
        _changer.DepositStatus.ShouldBe(CashDepositStatus.Count);

        // Act: 入金確定 (Fix)
        await _dispatcher.DispatchAsync("fix-deposit");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.Count);

        // Act: 入金終了 (End)
        await _dispatcher.DispatchAsync("end-deposit");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);

        // Assert: 在庫が反映されているか
        inventory.CalculateTotal().ShouldBeGreaterThan(0);
        _console.Output.ShouldContain("messages.deposit_started");
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.end_deposit_completed");
    }

    /// <summary>出金時、在高不足の場合に適切なエラーメッセージが表示されることを確認します。</summary>
    [Fact]
    public async Task DispenseWithInsufficientFundsShouldShowError()
    {
        // Arrange: 在庫が空の状態
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 1000円出金を試行
        await _dispatcher.DispatchAsync("dispense 1000");

        // Assert: エラーメッセージが含まれていること
        _console.Output.ShouldContain("[[messages.error_label]]");
    }

    /// <summary>対話型シェル経由で一連の入金操作を行い、最終的に終了することをテストします。</summary>
    [Fact]
    public async Task ShellDepositWorkflowShouldCompleteAndExit()
    {
        // Arrange
        var inputs = new Queue<string>(new[] { "deposit 500", "fix-deposit", "end-deposit", "exit" });
        _mockReader.Setup(r => r.Read(It.IsAny<string>())).Returns(() => inputs.Dequeue());
        
        // ConfirmExit() 用の入力を TestConsole に仕込む
        _console.Input.PushKey(ConsoleKey.Y);
        _console.Input.PushKey(ConsoleKey.Enter);
        
        // シェルを構成
        var shell = new CliInteractiveShell(
            _dispatcher,
            _changer,
            _console,
            _serviceProvider.GetRequiredService<IStringLocalizer>(),
            _serviceProvider.GetRequiredService<CliSessionOptions>(), // DIから取得
            _mockReader.Object);

        // Act
        await shell.RunAsync();

        // Assert
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.end_deposit_completed");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);
    }

    /// <summary>入金中に返却（Repay）を指示し、在庫が変化しないことを確認します。</summary>
    [Fact]
    public async Task RepayDepositShouldReturnCashCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");
        
        // 計数をシミュレート
        _changer.DepositController.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
        _changer.DepositAmount.ShouldBe(1000);

        // Act: 返却指示
        // 現在の CLI コマンド (end-deposit) は Change 固定であるため、
        // 統合テストとして返却（Repay）を検証するには、デバイスを直接操作するか、
        // CLI に Repay 機能が追加されるのを待つ必要があります。
        // ここではデバイスの RepayDeposit() を直接呼び出して、セッションが正しく終了し、
        // 在庫に反映されないことを検証します。
        _changer.RepayDeposit();

        // Assert
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);
        inventory.CalculateTotal().ShouldBe(0);
        // _console.Output.ShouldContain("messages.deposit_tray_label"); // CLI 経由でないため出力はされない
    }

    /// <summary>入金中に一時停止と再開を試行します。</summary>
    [Fact]
    public async Task PauseResumeDepositShouldWorkCorrectly()
    {
        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");
        _changer.DepositController.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));

        // UI等のコントローラーは DI で取得
        var depositController = _serviceProvider.GetRequiredService<DepositController>();
        
        // Act: 一時停止
        _changer.PauseDeposit(CashDepositPause.Pause);
        depositController.IsPaused.ShouldBeTrue();

        // Act: 再開
        _changer.PauseDeposit(CashDepositPause.Restart);
        depositController.IsPaused.ShouldBeFalse();
    }

    /// <summary>デバイスがジャム状態のときに入金を開始しようとしてエラーになることを確認します。</summary>
    [Fact]
    public async Task BeginDepositWhenJammedShouldShowError()
    {
        // Arrange: ジャム状態にする
        var hardwareStatus = _serviceProvider.GetRequiredService<HardwareStatusManager>();
        hardwareStatus.SetJammed(true);

        // Act: 入金開始試行
        await _dispatcher.DispatchAsync("deposit 1000");

        // Assert: エラーメッセージが表示されること
        _console.Output.ShouldContain("[[messages.error_label]]");
        _changer.DepositStatus.ShouldNotBe(CashDepositStatus.Count);
    }
}
