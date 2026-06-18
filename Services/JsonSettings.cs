using System.IO;
using System.Text.Json;

namespace WinMD.Services;

/// <summary>
/// Trwałe ustawienia w pliku JSON w <c>%LOCALAPPDATA%\WinMD\settings.json</c> (działa dla aplikacji
/// unpackaged — w przeciwieństwie do <c>ApplicationData.LocalSettings</c>, które wymaga tożsamości MSIX).
/// </summary>
public sealed class JsonSettings : ISettings
{
    private readonly string _path;
    private readonly Dictionary<string, bool> _bools;

    public JsonSettings()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinMD");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        _bools = Load();
    }

    public bool GetBool(string key, bool defaultValue)
        => _bools.TryGetValue(key, out var v) ? v : defaultValue;

    public void SetBool(string key, bool value)
    {
        _bools[key] = value;
        Save();
    }

    private Dictionary<string, bool> Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(_path)) ?? new();
        }
        catch { /* uszkodzony/niedostępny plik — użyj domyślnych */ }
        return new();
    }

    private void Save()
    {
        try { File.WriteAllText(_path, JsonSerializer.Serialize(_bools)); }
        catch { /* zapis nieobowiązkowy */ }
    }
}
