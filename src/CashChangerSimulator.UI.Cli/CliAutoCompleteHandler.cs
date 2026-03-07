namespace CashChangerSimulator.UI.Cli;

/// <summary>CLI インタラクティブモード用のオートコンプリートハンドラ。</summary>
/// <remarks>
/// コマンドリストに基づき、入力された文字列の前方一致によるサジェストを提供します。
/// </remarks>
public class CliAutoCompleteHandler(string[] commands) : IAutoCompleteHandler
{
    private readonly string[] _commands = commands;

    /// <summary>単語の区切り文字を設定または取得します。</summary>
    public char[] Separators { get; set; } = [' '];

    /// <summary>入力テキストに対するサジェストリストを取得します。</summary>
    /// <param name="text">現在の入力テキスト。</param>
    /// <param name="index">カーソル位置（未使用）。</param>
    /// <returns>一致するコマンドの配列。</returns>
    public string[] GetSuggestions(string text, int index)
    {
        return string.IsNullOrWhiteSpace(text) ? _commands : [.. _commands.Where(c => c.StartsWith(text, StringComparison.OrdinalIgnoreCase))];
    }
}
