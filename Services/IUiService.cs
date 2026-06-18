namespace WinMD.Services;

/// <summary>
/// Abstrakcja operacji UI używanych przez <c>EditorViewModel</c> (dialogi, toast, motyw, pomoc,
/// udostępnianie, timer). Implementacja żyje w warstwie WinUI 3 (okno), dzięki czemu ViewModel
/// pozostaje niezależny od konkretnych kontrolek.
/// </summary>
public interface IUiService
{
    /// <summary>Pyta tak/nie. Zwraca true, gdy wybrano akcję potwierdzającą.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Komunikat informacyjny z jednym przyciskiem.</summary>
    Task AlertAsync(string title, string message, string ok = "OK");

    /// <summary>Prosi o tekst. Zwraca null, gdy anulowano.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string cancel, string initialValue);

    /// <summary>Lista wyborów. Zwraca etykietę wybraną przez użytkownika lub null.</summary>
    Task<string?> ChooseAsync(string title, string cancel, params string[] options);

    /// <summary>Krótki, samoznikający komunikat statusu.</summary>
    void Toast(string message);

    /// <summary>Ustawia motyw aplikacji (jasny/ciemny).</summary>
    void ApplyTheme(bool dark);

    /// <summary>Otwiera okno pomocy.</summary>
    void GoToHelp();

    /// <summary>Udostępnia plik systemowym arkuszem udostępniania.</summary>
    Task ShareFileAsync(string path, string title);

    /// <summary>Tworzy jednorazowy timer (po Start odlicza interval i woła onTick; Stop anuluje).</summary>
    ITimerLite CreateTimer(TimeSpan interval, Action onTick);
}

/// <summary>Minimalny, restartowalny timer (odpowiednik MAUI IDispatcherTimer, IsRepeating=false).</summary>
public interface ITimerLite
{
    void Start();
    void Stop();
}
