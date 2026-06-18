namespace WinMD.Services;

/// <summary>
/// Eksportuje wyrenderowany dokument HTML do pliku PDF.
/// </summary>
public interface IPdfExporter
{
    /// <summary>
    /// Renderuje przekazany HTML i uruchamia eksport do PDF
    /// (na Androidzie przez systemowy mechanizm drukowania → „Zapisz jako PDF").
    /// </summary>
    /// <param name="html">Kompletny dokument HTML do wydruku.</param>
    /// <param name="jobName">Nazwa zadania / sugerowana nazwa pliku (bez rozszerzenia).</param>
    /// <returns><c>true</c>, jeśli eksport został uruchomiony.</returns>
    Task<bool> ExportAsync(string html, string jobName);

    /// <summary>
    /// Renderuje HTML do pliku PDF (po cichu, bez dialogu druku) i zwraca ścieżkę do pliku.
    /// Używane do udostępniania PDF.
    /// </summary>
    /// <param name="html">Kompletny dokument HTML.</param>
    /// <param name="fileName">Nazwa pliku PDF (z rozszerzeniem .pdf).</param>
    /// <returns>Ścieżka do wygenerowanego pliku lub <c>null</c> przy błędzie.</returns>
    Task<string?> RenderToFileAsync(string html, string fileName);
}
