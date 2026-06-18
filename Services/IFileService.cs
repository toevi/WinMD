using WinMD.Models;

namespace WinMD.Services;

/// <summary>
/// Abstrakcja dostępu do plików .md. Implementacja na Androidzie używa
/// Storage Access Framework (SAF) z trwałymi uprawnieniami do URI.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Pozwala użytkownikowi wybrać istniejący plik .md i wczytuje jego treść.
    /// </summary>
    /// <returns>Wczytany dokument lub <c>null</c>, gdy użytkownik anulował wybór.</returns>
    Task<MarkdownDocument?> OpenAsync();

    /// <summary>
    /// Wczytuje plik wskazany przez zewnętrzny URI (np. z systemowego „Otwórz za pomocą"),
    /// z pominięciem pickera. Próbuje utrwalić uprawnienia, aby umożliwić późniejszy zapis.
    /// </summary>
    /// <param name="uri">URI pliku (content://… lub file://…).</param>
    /// <returns>Wczytany dokument lub <c>null</c>, gdy odczyt się nie powiódł.</returns>
    Task<MarkdownDocument?> OpenFromUriAsync(string uri);

    /// <summary>
    /// Zapisuje <see cref="MarkdownDocument.Content"/> z powrotem do pliku
    /// wskazanego przez <see cref="MarkdownDocument.Identifier"/> (nadpisanie).
    /// </summary>
    /// <returns>True, jeśli zapis się powiódł.</returns>
    Task<bool> SaveAsync(MarkdownDocument document);

    /// <summary>
    /// Tworzy nowy plik .md w lokalizacji wybranej przez użytkownika i zapisuje w nim treść.
    /// </summary>
    /// <param name="suggestedName">Sugerowana nazwa pliku (np. "Bez nazwy.md").</param>
    /// <param name="content">Treść początkowa do zapisania.</param>
    /// <returns>Utworzony dokument z ustawionym <see cref="MarkdownDocument.Identifier"/>,
    /// lub <c>null</c>, gdy użytkownik anulował.</returns>
    Task<MarkdownDocument?> CreateAsync(string suggestedName, string content);

    /// <summary>
    /// Zmienia nazwę pliku. Po powodzeniu aktualizuje <see cref="MarkdownDocument.Identifier"/>
    /// i <see cref="MarkdownDocument.FileName"/> przekazanego dokumentu.
    /// </summary>
    /// <returns>True, jeśli zmiana nazwy się powiodła.</returns>
    Task<bool> RenameAsync(MarkdownDocument document, string newName);

    /// <summary>
    /// Eksportuje dowolną treść tekstową do nowego pliku wybranego przez użytkownika
    /// (np. wyrenderowany HTML). Nie zmienia bieżącego dokumentu.
    /// </summary>
    /// <param name="suggestedName">Sugerowana nazwa pliku (z rozszerzeniem, np. "raport.html").</param>
    /// <param name="mimeType">Typ MIME tworzonego pliku (np. "text/html").</param>
    /// <param name="content">Treść do zapisania.</param>
    /// <returns>True, jeśli plik został utworzony i zapisany.</returns>
    Task<bool> ExportTextAsync(string suggestedName, string mimeType, string content);
}
