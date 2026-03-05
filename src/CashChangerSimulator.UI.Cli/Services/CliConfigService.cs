using System.Reflection;
using System.Collections;
using Spectre.Console;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.UI.Cli.Services;

public class CliConfigService(
    ConfigurationProvider configProvider,
    IAnsiConsole console,
    IStringLocalizer localizer) : CliServiceBase(console, localizer)
{
    private readonly ConfigurationProvider _configProvider = configProvider;

    public void List()
    {
        _console.Write(new Rule($"[cyan]{_L["ConfigHeader"]}[/]").LeftJustified());
        PrintObject(_configProvider.Config, "");
    }

    private void PrintObject(object obj, string prefix)
    {
        var type = obj.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(obj);
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            if (value == null)
            {
                _console.MarkupLine($"{key} = [grey]null[/]");
            }
            else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType.IsEnum)
            {
                _console.MarkupLine($"{key} = [green]{value}[/]");
            }
            else if (value is IDictionary)
            {
                _console.MarkupLine($"{key} = [grey](Dictionary)[/]");
            }
            else
            {
                PrintObject(value, key);
            }
        }
    }

    public void Get(string key)
    {
        var result = GetPropertyByPath(_configProvider.Config, key);
        if (result.Success)
        {
            _console.MarkupLine($"{key} = [green]{result.Value}[/]");
        }
        else
        {
            _console.MarkupLine(_L["InvalidConfigKey", key]);
        }
    }

    public void Set(string key, string value)
    {
        var result = SetPropertyByPath(_configProvider.Config, key, value);
        if (result)
        {
            _console.MarkupLine(_L["ConfigUpdated", key, value]);
        }
        else
        {
            _console.MarkupLine(_L["InvalidConfigKey", key]);
        }
    }

    public void Save()
    {
        try
        {
            ConfigurationLoader.Save(_configProvider.Config);
            _console.MarkupLine(_L["ConfigSaved"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public void Reload()
    {
        try
        {
            _configProvider.Reload();
            _console.MarkupLine(_L["ConfigReloaded"]);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private static (bool Success, object? Value) GetPropertyByPath(object obj, string path)
    {
        var parts = path.Split('.');
        object? current = obj;

        foreach (var part in parts)
        {
            if (current == null) return (false, null);
            var prop = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return (false, null);
            current = prop.GetValue(current);
        }

        return (true, current);
    }

    private static bool SetPropertyByPath(object obj, string path, string value)
    {
        var parts = path.Split('.');
        object? current = obj;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (current == null) return false;
            var prop = current.GetType().GetProperty(parts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null) return false;
            current = prop.GetValue(current);
        }

        if (current == null) return false;
        var finalProp = current.GetType().GetProperty(parts.Last(), BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (finalProp == null || !finalProp.CanWrite) return false;

        try
        {
            object? convertedValue = finalProp.PropertyType.IsEnum
                ? Enum.Parse(finalProp.PropertyType, value, true)
                : Convert.ChangeType(value, finalProp.PropertyType);
            finalProp.SetValue(current, convertedValue);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
