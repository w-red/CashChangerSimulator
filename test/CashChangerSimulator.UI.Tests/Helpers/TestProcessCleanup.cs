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
        var processes = Process.GetProcessesByName(ProcessName);
        foreach (var p in processes)
        {
            try
            {
                // UIプロセスが残っていると、次のテストでの Claim に失敗するため強制終了する
                p.Kill(true);
                p.WaitForExit(5000);
            }
            catch
            {
                // 既に終了している場合などは無視
            }
        }
    }
}
