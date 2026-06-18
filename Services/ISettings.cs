namespace WinMD.Services;

/// <summary>Trwałe ustawienia użytkownika (np. wybór motywu). Implementacja: plik JSON w LocalAppData.</summary>
public interface ISettings
{
    bool GetBool(string key, bool defaultValue);
    void SetBool(string key, bool value);
}
