using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Tomlyn;
using Tomlyn.Model;

namespace CashChangerSimulator.UI.Cli.Localization;

/// <summary>TOML ファイルを使用してローカライズされた文字列を提供するローカライザー。</summary>
/// <remarks>
/// 文化（Culture）に応じた TOML ファイル（cli.{lang}.toml）を読み込みます。
/// ドット区切りのキー名（例: "messages.welcome"）によるネストされたテーブルの検索をサポートします。
/// </remarks>
public class TomlStringLocalizer(string basePath) : IStringLocalizer
{
    private readonly ConcurrentDictionary<string, TomlTable> _cache = new();
    private readonly string _basePath = basePath;

    /// <summary>指定された名前のローカライズされた文字列を取得します。</summary>
    /// <param name="name">取得する文字列の名前。</param>
    /// <returns>LocalizedString オブジェクト。見つからない場合はキー名が返されます。</returns>
    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

    /// <summary>引数でフォーマットされたローカライズされた文字列を取得します。</summary>
    /// <param name="name">取得する文字列の名前。</param>
    /// <param name="arguments">フォーマットに使用する引数。</param>
    /// <returns>フォーマット済みの LocalizedString オブジェクト。</returns>
    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value != null ? string.Format(value, arguments) : name, resourceNotFound: value == null);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var table = GetCurrentTable();
        if (table == null) yield break;

        foreach (var entry in table)
        {
            if (entry.Value is TomlTable subTable)
            {
                foreach (var subEntry in subTable)
                {
                    yield return new LocalizedString($"{entry.Key}.{subEntry.Key}", subEntry.Value?.ToString() ?? string.Empty, resourceNotFound: false);
                }
            }
            else
            {
                yield return new LocalizedString(entry.Key, entry.Value?.ToString() ?? string.Empty, resourceNotFound: false);
            }
        }
    }

    private string? GetString(string name)
    {
        var table = GetCurrentTable();
        if (table == null) return null;

        var parts = name.Split('.');
        object? current = table;

        foreach (var part in parts)
        {
            if (current is TomlTable t && t.TryGetValue(part, out var next))
            {
                current = next;
            }
            else
            {
                return null;
            }
        }

        return current?.ToString();
    }

    private TomlTable? GetCurrentTable()
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(culture)) culture = "en-US";

        if (_cache.TryGetValue(culture, out var cached)) return cached;

        // Try exact match first
        var path = Path.Combine(_basePath, $"cli.{culture}.toml");
        if (!File.Exists(path))
        {
            // Fallback to language only (e.g. ja-JP -> ja)
            var lang = culture.Split('-')[0];
            path = Path.Combine(_basePath, $"cli.{lang}.toml");
            
            // Further fallback to en-US
            if (!File.Exists(path))
            {
                path = Path.Combine(_basePath, "cli.en.toml");
            }
        }

        if (!File.Exists(path)) return null;

        try
        {
            var tomlText = File.ReadAllText(path);
            var table = TomlSerializer.Deserialize<TomlTable>(tomlText) ?? [];
            _cache[culture] = table;
            return table;
        }
        catch
        {
            return null;
        }
    }
}
