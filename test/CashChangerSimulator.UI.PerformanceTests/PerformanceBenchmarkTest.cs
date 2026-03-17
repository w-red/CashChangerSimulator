using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.UI.Tests.Fixtures;
using System.Diagnostics;
using Shouldly;
using CashChangerSimulator.Core;

namespace CashChangerSimulator.UI.PerformanceTests;

/// <summary>取引フローのパフォーマンスを計測するベンチマークテスト。</summary>
[Collection("SequentialTests")]
public class PerformanceBenchmarkTest(UIViewModelFixture fixture) : IClassFixture<UIViewModelFixture>
{
    private readonly UIViewModelFixture _fixture = fixture;

    static PerformanceBenchmarkTest()
    {
        // Ensure Logging is initialized for the benchmark
        LogProvider.Initialize(new LoggingSettings
        {
            LogLevel = "Information",
            EnableConsole = false, // Outputting to console during benchmark is slow
            EnableFile = false
        });
    }

    [Fact]
    public void Benchmark10000Transactions()
    {
        var changer = _fixture.CashChanger;
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        const int iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            changer.BeginDeposit();
            // Simulate some deposits
            _fixture.DepositController.TrackDeposit(new DenominationKey(100, CurrencyCashType.Coin, "JPY"));
            changer.FixDeposit();
            changer.EndDeposit(Microsoft.PointOfService.CashDepositAction.NoChange);
        }

        sw.Stop();

        var totalMs = sw.ElapsedMilliseconds;
        _ = (double)totalMs / iterations;

        // Assert some reasonable performance (e.g., < 10s for 10k operations in-memory)
        totalMs.ShouldBeLessThan(10000, "10,000 transactions should be processed within 10 seconds in simulator.");
    }
}
