namespace CashChangerSimulator.UI.Tests.Helpers;

/// <summary>並列実行を無効化し、順次実行が必要なテストをグループ化するためのコレクション定義クラス。</summary>
[CollectionDefinition("SequentialTests", DisableParallelization = true)]
public class SequentialTestsCollection
{
}
