using System.Diagnostics;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>
/// テスト実行環境をクリーンニングするためのヘルパークラス。
/// </summary>
public static class TestProcessCleanup
{
    private const string ProcessName = "CashChangerSimulator.UI.Wpf";

    /// <summary>
    /// 実行中のすべての CashChangerSimulator.UI.Wpf プロセスを強制終了します。
    /// </summary>
    public static void KillAllRunningProcesses()
    {
        var targetNames = new[] { ProcessName, "CashChangerSimulator.UI.Wpf.exe" };
        
        foreach (var name in targetNames)
        {
            var processes = Process.GetProcessesByName(name.Replace(".exe", ""));
            foreach (var p in processes)
            {
                try
                {
                    // UIプロセスが残っていると、次のテストでの Claim に失敗するため強制終了する
                    p.Kill(true);
                    p.WaitForExit(2000);
                }
                catch { }
            }
        }

        // Fallback for stubbornly remaining processes using CLI
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /IM {ProcessName}.exe /T",
                CreateNoWindow = true,
                UseShellExecute = false
            });
            proc?.WaitForExit(2000);
        }
        catch { }
    }
}
