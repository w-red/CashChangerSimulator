namespace CashChangerSimulator.UI.Cli.Services;

/// <summary>コマンドのディスパッチ（振り分け）を行うインターフェース。</summary>
public interface ICliCommandDispatcher
{
    /// <summary>入力文字列を解析し、対応するコマンドを実行します。</summary>
    /// <param name="line">入力文字列。</param>
    Task DispatchAsync(string line);
}
