using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.UI.Cli.Localization;

/// <summary>TOML 形式のローカライゼーションサービスを作成するファクトリ。</summary>
public class TomlStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly string _basePath;

    /// <summary>ファクトリのインスタンスを初期化します。</summary>
    public TomlStringLocalizerFactory()
    {
        _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
    }

    /// <summary>指定された型のリソースを処理するローカライザーを作成します。</summary>
    /// <param name="resourceSource">リソースのソースとなる型。</param>
    /// <returns>TomlStringLocalizer のインスタンス。</returns>
    public IStringLocalizer Create(Type resourceSource) => new TomlStringLocalizer(_basePath);

    /// <summary>指定された名前と場所のリソースを処理するローカライザーを作成します。</summary>
    /// <param name="baseName">リソースのベース名。</param>
    /// <param name="location">リソースの場所。</param>
    /// <returns>TomlStringLocalizer のインスタンス。</returns>
    public IStringLocalizer Create(string baseName, string location) => new TomlStringLocalizer(_basePath);
}
