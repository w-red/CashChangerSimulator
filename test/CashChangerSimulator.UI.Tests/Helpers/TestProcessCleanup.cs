using System.Diagnostics;

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>テスト実行環境をクリーンアップするためのヘルパークラス。</summary>
public static class TestProcessCleanup
{
    private const string ProcessName = "CashChangerSimulator.UI.Wpf";

    /// <summary>実行中のすべてのシミュレータ関連プロセスを強制終了します。</summary>
    public static void KillAllRunningProcesses()
    {
        // 開発・テスト対象のアプリ名のみをターゲットにする。
        // testhost や .Tests.exe を含めると、並列実行中の他のテストランナーを殺してしまうため。
        var targetNames = new[] { "CashChangerSimulator.UI.Wpf", "CashChangerSimulator.UI.Cli", "CashChangerSimulator.UI" };
        
        foreach (var name in targetNames)
        {
            try
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
            catch { }
        }

        // Fallback for stubbornly remaining processes using CLI
        try
        {
            foreach (var name in targetNames)
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/F /IM {name}.exe /T",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                proc?.WaitForExit(2000);
            }
        }
        catch { }
    }
}
