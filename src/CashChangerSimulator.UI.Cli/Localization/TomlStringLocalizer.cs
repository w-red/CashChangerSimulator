using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Localization;
using Tomlyn;
using Tomlyn.Model;

namespace CashChangerSimulator.UI.Cli.Localization;

public class TomlStringLocalizer : IStringLocalizer
{
    private static readonly ConcurrentDictionary<string, TomlTable> _cache = new();
    private readonly string _basePath;

    public TomlStringLocalizer(string basePath)
    {
        _basePath = basePath;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var value = GetString(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value == null);
        }
    }

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

        foreach (var pair in table)
        {
            var value = pair.Value?.ToString() ?? string.Empty;
            yield return new LocalizedString(pair.Key, value, resourceNotFound: false);
        }
    }

    private string? GetString(string name)
    {
        var table = GetCurrentTable();
        return table != null && table.TryGetValue(name, out var value) ? (value?.ToString()) : null;
    }

    private TomlTable? GetCurrentTable()
    {
        var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        // Fallback to English if not Japanese
        if (culture != "ja" && culture != "en") culture = "en";

        var cacheKey = culture;
        if (_cache.TryGetValue(cacheKey, out var cached)) return cached;

        var path = Path.Combine(_basePath, $"{culture}.toml");
        if (!File.Exists(path)) return null;

        try
        {
            var tomlText = File.ReadAllText(path);
            var table = TomlSerializer.Deserialize<TomlTable>(tomlText) ?? new TomlTable();
            _cache[cacheKey] = table;
            return table;
        }
        catch
        {
            return null;
        }
    }
}

public class TomlStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly string _basePath;

    public TomlStringLocalizerFactory()
    {
        _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "i18n");
    }

    public IStringLocalizer Create(Type resourceSource) => new TomlStringLocalizer(_basePath);

    public IStringLocalizer Create(string baseName, string location) => new TomlStringLocalizer(_basePath);
}
