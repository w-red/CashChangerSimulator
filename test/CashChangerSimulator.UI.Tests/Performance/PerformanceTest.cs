using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using MoneyKind4Opos.Currencies.Interfaces;
using System.Diagnostics;
using System.IO;

namespace CashChangerSimulator.UI.Tests.Performance;

public class PerformanceTest(ITestOutputHelper output)
{
    [Fact]
    public void BulkTransactionPerformance()
    {
        // Setup
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);

        // JPY setup
        var key1000 = new DenominationKey(1000m, CashType.Bill, "JPY");
        inventory.SetCount(key1000, 10000);

        const int iterations = 10000;
        output.WriteLine($"Starting bulk dispense test with {iterations} iterations...");

        var sw = Stopwatch.StartNew();
        var initialMemory = GC.GetTotalMemory(true);

        for (int i = 0; i < iterations; i++)
        {
            manager.Dispense(1000m);
        }

        sw.Stop();
        var finalMemory = GC.GetTotalMemory(true);

        output.WriteLine($"Completed {iterations} dispense operations.");
        output.WriteLine($"Total Time: {sw.ElapsedMilliseconds} ms");
        output.WriteLine($"Avg Time per op: {(double)sw.ElapsedMilliseconds / iterations:F4} ms");
        output.WriteLine($"Memory Delta: {(finalMemory - initialMemory) / 1024.0:F2} KB");
    }

    [Fact]
    public void LoggingOverheadAnalysis()
    {
        const int iterations = 50000;

        // Scenario 1: No Logging
        LogProvider.Initialize(new LoggingSettings { EnableConsole = false, EnableFile = false, LogLevel = "None" });
        var timeNoLog = MeasureLogTime(iterations, "No Logging");

        // Scenario 2: ZLogger Console (High overhead usually)
        LogProvider.Initialize(new LoggingSettings { EnableConsole = true, EnableFile = false, LogLevel = "Information" });
        var timeConsoleLog = MeasureLogTime(iterations, "ZLogger Console");

        // Scenario 3: ZLogger File
        var tempLogDir = Path.Combine(Path.GetTempPath(), "CCS_PerfTest");
        LogProvider.Initialize(new LoggingSettings
        {
            EnableConsole = false,
            EnableFile = true,
            LogLevel = "Information",
            LogDirectory = tempLogDir,
            LogFileName = "perf_test.log"
        });
        var timeFileLog = MeasureLogTime(iterations, "ZLogger File");

        output.WriteLine("--- Performance Summary ---");
        output.WriteLine($"Iterations: {iterations}");
        output.WriteLine($"No Logging: {timeNoLog} ms");
        output.WriteLine($"Console Log: {timeConsoleLog} ms");
        output.WriteLine($"File Log: {timeFileLog} ms");

        try
        {
            if (Directory.Exists(tempLogDir)) Directory.Delete(tempLogDir, true);
        }
        catch { /* Ignore cleanup errors in performance tests */ }
    }

    private long MeasureLogTime(int iterations, string label)
    {
        var logger = LogProvider.CreateLogger<PerformanceTest>();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            logger.LogInformation("Test log message with index {Index}", i);
        }
        sw.Stop();
        output.WriteLine($"{label}: {sw.ElapsedMilliseconds} ms");
        return sw.ElapsedMilliseconds;
    }
}
