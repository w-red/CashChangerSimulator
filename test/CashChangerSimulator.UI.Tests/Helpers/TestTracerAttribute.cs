using System.Reflection;
using Xunit.v3;

[assembly: CashChangerSimulator.UI.Tests.Helpers.TestTracer]

namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>
/// 各テストの開始時に実行中のテスト名を C:\Logs\current_test.txt に書き出すトレーサー。
/// サンドボックス内からホスト側の監視スクリプトへの干渉を避けるため FileShare.ReadWrite を使用します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class TestTracerAttribute : BeforeAfterTestAttribute
{
    private const string LogPath = @"C:\Logs\current_test.txt";
    private const string LogDir = @"C:\Logs";

    /// <summary>
    /// テスト開始前に実行されるフック。
    /// </summary>
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        try
        {
            // ディレクトリの存在を確認
            if (!Directory.Exists(LogDir))
            {
                Directory.CreateDirectory(LogDir);
            }

            // 競合を避けるために FileShare.ReadWrite で開き、テスト名を上書き
            using var fs = new FileStream(LogPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var sw = new StreamWriter(fs);
            sw.Write(test.TestDisplayName);
            sw.Flush();
        }
        catch
        {
            // トレーサーの不具合でテストが落ちないよう、例外は無視する
        }
    }
}
