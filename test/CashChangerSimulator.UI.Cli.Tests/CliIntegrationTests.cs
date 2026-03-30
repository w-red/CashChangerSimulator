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
        mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string s) => new LocalizedString(s, s));
        mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string s, object[] args) => 
            new LocalizedString(s, args != null && args.Length > 0 ? $"{s} {string.Join(" ", args)}" : s));
        services.AddSingleton<IStringLocalizer>(mockLocalizer.Object);

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
    public async Task DepositCycle_ShouldIncreaseInventoryCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        var tenYen = new DenominationKey(10, CurrencyCashType.Coin, "JPY");
        inventory.GetCount(tenYen).ShouldBe(0);

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 100"); // 100円分
        
        // 入金中の状態を確認
        _changer.DepositStatus.ShouldBe(CashDepositStatus.Count);

        // Act: 入金確定 (Fix)
        await _dispatcher.DispatchAsync("fix-deposit");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.Count); // Fix 後もステータスは Count (または Start)

        // Act: 入金終了 (End)
        await _dispatcher.DispatchAsync("end-deposit");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);

        // Assert: 在庫が反映されているか
        inventory.CalculateTotal().ShouldBeGreaterThan(0);
        _console.Output.ShouldContain("messages.deposit_started");
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.deposit_ended");
    }

    /// <summary>出金時、在高不足の場合に適切なエラーメッセージが表示されることを確認します。</summary>
    [Fact]
    public async Task Dispense_WithInsufficientFunds_ShouldShowError()
    {
        // Arrange: 在庫が空の状態
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 1000円出金を試行
        await _dispatcher.DispatchAsync("dispense 1000");

        // Assert: エラーメッセージが含まれていること
        _console.Output.ShouldContain("messages.error_prefix");
    }

    /// <summary>対話型シェル経由で一連の入金操作を行い、最終的に終了することをテストします。</summary>
    [Fact]
    public async Task Shell_DepositWorkflow_ShouldCompleteAndExit()
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
            new CliSessionOptions { IsAsync = false },
            _mockReader.Object);

        // Act
        await shell.RunAsync();

        // Assert
        _console.Output.ShouldContain("messages.deposit_started 500");
        _console.Output.ShouldContain("messages.deposit_fixed");
        _console.Output.ShouldContain("messages.deposit_ended");
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);
    }

    /// <summary>入金中に返却（Repay）を指示し、在庫が変化しないことを確認します。</summary>
    [Fact]
    public async Task RepayDeposit_ShouldReturnCashCorrectly()
    {
        // Arrange
        var inventory = _serviceProvider.GetRequiredService<Inventory>();
        inventory.Clear();

        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");
        _changer.DepositAmount.ShouldBe(1000);

        // Act: 返却指示
        await _dispatcher.DispatchAsync("repay-deposit");

        // Assert
        _changer.DepositStatus.ShouldBe(CashDepositStatus.End);
        inventory.CalculateTotal().ShouldBe(0);
        _console.Output.ShouldContain("messages.deposit_repaid");
    }

    /// <summary>入金中に一時停止と再開を試行します。</summary>
    [Fact]
    public async Task PauseResumeDeposit_ShouldWorkCorrectly()
    {
        // Act: 入金開始
        await _dispatcher.DispatchAsync("deposit 1000");

        // Act: 一時停止
        await _dispatcher.DispatchAsync("pause-deposit");
        var depositController = _serviceProvider.GetRequiredService<DepositController>();
        depositController.IsPaused.ShouldBeTrue();

        // Act: 再開
        await _dispatcher.DispatchAsync("resume-deposit");
        depositController.IsPaused.ShouldBeFalse();

        _console.Output.ShouldContain("messages.deposit_paused");
        _console.Output.ShouldContain("messages.deposit_resumed");
    }

    /// <summary>デバイスがジャム状態のときに入金を開始しようとしてエラーになることを確認します。</summary>
    [Fact]
    public async Task BeginDeposit_WhenJammed_ShouldShowError()
    {
        // Arrange: ジャム状態にする
        var hardwareStatus = _serviceProvider.GetRequiredService<HardwareStatusManager>();
        hardwareStatus.SetJammed(true);

        // Act: 入金開始試行
        await _dispatcher.DispatchAsync("deposit 1000");

        // Assert: エラーメッセージが表示されること
        _console.Output.ShouldContain("messages.error_prefix");
        _changer.DepositStatus.ShouldNotBe(CashDepositStatus.Count);
    }
}
