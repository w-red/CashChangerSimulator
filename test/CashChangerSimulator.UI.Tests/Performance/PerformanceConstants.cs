namespace CashChangerSimulator.UI.Tests.Performance;

/// <summary>パフォーマンステスト用の定数設定。</summary>
public static class PerformanceConstants
{
    /// <summary>一括出金テストの反復回数。</summary>
    public const int BulkDispenseIterations = 10000;

    /// <summary>ロギングオーバーヘッド分析の反復回数。</summary>
    public const int LoggingAnalysisIterations = 50000;

    /// <summary>テスト用の初期在庫枚数。</summary>
    public const int InitialInventoryCount = 10000;

    /// <summary>テスト用の出金金額。</summary>
    public const decimal DispenseAmount = 1000m;
}
