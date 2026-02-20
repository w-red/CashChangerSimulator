# コード分析・リファクタリング機会レポート

**生成日時**: 2026年2月20日  
**対象**: CashChangerSimulator ソリューション（.NET 10）  
**分析対象**: 全プロジェクト

---

## 📋 目次

1. [優先度: 高](#優先度-高)
2. [優先度: 中](#優先度-中)
3. [優先度: 低](#優先度-低)
4. [実装チェックリスト](#実装チェックリスト)

---

## 優先度: 高

### 1. マジックナンバーの定数化（タイミング待機）

**ファイル**: 複数箇所  
**対象ファイル**:
- `test/CashChangerSimulator.Tests/DispenseControllerTest.cs`
- `test/CashChangerSimulator.UI.Tests/Specs/DepositTest.cs`
- `test/CashChangerSimulator.UI.Tests/Performance/PerformanceTest.cs`

**現在のコード**:
\`\`\`
await Task.Delay(300, TestContext.Current.CancellationToken);
await Task.Delay(50, TestContext.Current.CancellationToken);
Thread.Sleep(1000);
Thread.Sleep(2000);
Thread.Sleep(500);
\`\`\`

**改善案**:

\`\`\`csharp
public static class TestTimingConstants
{
    /// <summary>シミュレーション完了待機時間。</summary>
    public const int CompletionWaitMs = 300;

    /// <summary>ディスペンス開始確認待機時間。</summary>
    public const int StartupCheckDelayMs = 50;

    /// <summary>UI状態遷移待機時間。</summary>
    public const int UiTransitionDelayMs = 1000;

    /// <summary>ウィンドウポップアップ待機時間。</summary>
    public const int WindowPopupDelayMs = 2000;

    /// <summary>UI論理実行待機時間。</summary>
    public const int LogicExecutionDelayMs = 500;
}
\`\`\`

---

### 2. 空のコールバック処理の標準化

**ファイル**: `test/CashChangerSimulator.Tests/DispenseControllerTest.cs`

**改善案**:

\`\`\`csharp
/// <summary>ディスペンス結果を無視するコールバック。</summary>
private static void IgnoreDispenseResult(ErrorCode code, int codeEx) { }

// 使用例
_ = controller.DispenseChangeAsync(1000, true, IgnoreDispenseResult);
\`\`\`

---

### 3. UIテスト遅延処理の定数化

**ファイル**: `test/CashChangerSimulator.UI.Tests/Specs/DepositTest.cs`

**改善案**:

\`\`\`csharp
private static class DepositTestTimings
{
    public const int UITransitionMs = 1000;
    public const int WindowCloseMs = 2000;
    public const int ModeTransitionMs = 500;
    
    public static readonly TimeSpan RetryLongTimeout = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan RetryShortTimeout = TimeSpan.FromSeconds(5);
}
\`\`\`

---

## 優先度: 中

### 4. SimulationSettings の検証強化

**ファイル**: `src/CashChangerSimulator.Core/Configuration/SimulationSettings.cs`

検証ロジックをプロパティゲッター/セッターに追加してください。

---

### 5. DispenseController の遅延処理ロジック抽出

**ファイル**: `src/CashChangerSimulator.Device/DispenseController.cs`

シミュレーション処理をビジネスロジックから分離してください。

---

### 6. UITestRetry の改善と文書化

**ファイル**: `test/CashChangerSimulator.UI.Tests/Specs/UiTestRetry.cs`

Null-safety を向上させてください。

---

## 優先度: 低

### 7. DebugDumpTest の出力形式改善

**ファイル**: `test/CashChangerSimulator.UI.Tests/Specs/DebugDumpTest.cs`

構造化出力（JSON形式）の検討を行ってください。

---

### 8. Setup-Device.ps1 のエラーハンドリング強化

**ファイル**: `scripts/Setup-Device.ps1`

トラップハンドラーを追加してください。

---

## 📊 実装チェックリスト

### 第1段階（即座実施）
- [ ] TestTimingConstants クラス作成
- [ ] IgnoreDispenseResult ヘルパーメソッド追加
- [ ] DispenseControllerTest.cs の更新
- [ ] DepositTest.cs の遅延時間定数化

### 第2段階（次回スプリント）
- [ ] SimulationSettings の検証強化
- [ ] DispenseController シミュレーション処理の分離
- [ ] UiTestRetry の改善
- [ ] 単体テスト追加

### 第3段階（セキュリティ・保守性）
- [ ] DebugDumpTest の出力形式改善
- [ ] Setup-Device.ps1 のエラーハンドリング強化
- [ ] 統合テストの追加

---

## 📊 影響度分析

| リファクタリング | ファイル数 | 影響範囲 | 実装工数 |
|------------------|-----------|---------|---------|
| マジックナンバー定数化 | 3 | テスト層 | 低（30min） |
| コールバック標準化 | 1 | テスト層 | 低（20min） |
| UIテスト遅延定数化 | 1 | テスト層 | 低（40min） |
| SimulationSettings検証 | 1 | コア層 | 中（2h） |
| DispenseController分離 | 1 | デバイス層 | 中（1.5h） |
| UITestRetry改善 | 1 | テスト層 | 中（1h） |
| DebugDumpTest改善 | 1 | テスト層 | 中（1.5h） |
| Setup-Device.ps1改善 | 1 | スクリプト層 | 中（1h） |

**合計推定工数**: 約 8.5時間

---

**生成者**: GitHub Copilot  
**対象フレームワーク**: .NET 10  
**最終更新**: 2026年2月20日
"@ | Out-File -Encoding UTF8 "REFACTORING_OPPORTUNITIES.md"

Write-Host "✅ REFACTORING_OPPORTUNITIES.md を作成しました" -ForegroundColor Green
Get-Item "REFACTORING_OPPORTUNITIES.md"