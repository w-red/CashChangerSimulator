using System.Runtime.CompilerServices;

namespace CashChangerSimulator.UI.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init()
    {
        VerifyImageMagick.Initialize();
        // アニメーションや時計など動的要素の微小な差異を許容するため、比較許容差を 5% に設定
        VerifyImageMagick.RegisterComparers(0.05);
        
        // ファイルロック問題を回避し、CI環境でも安全に実行するため DiffEngine を無効化
        DiffEngine.DiffRunner.Disabled = true;
    }
}
