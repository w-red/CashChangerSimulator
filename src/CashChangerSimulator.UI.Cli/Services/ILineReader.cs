namespace CashChangerSimulator.UI.Cli.Services;

/// <summary>行単位の入力を抽象化するインターフェース。</summary>
public interface ILineReader
{
    /// <summary>プロンプトを表示して一行読み取ります。</summary>
    string Read(string prompt);
    
    /// <summary>履歴を入力に追加します。</summary>
    void AddHistory(params string[] history);
}

/// <summary>ReadLine ライブラリを使用した <see cref="ILineReader"/> の実装。</summary>
public class ReadLineReader : ILineReader
{
    public string Read(string prompt) => ReadLine.Read(prompt);
    public void AddHistory(params string[] history) => ReadLine.AddHistory(history);
}
