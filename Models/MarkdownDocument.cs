namespace WinMD.Models;

/// <summary>
/// Reprezentuje pojedynczy dokument Markdown otwarty w aplikacji.
/// Nośnik danych współdzielony między warstwą plików, ViewModel i UI.
/// </summary>
public sealed class MarkdownDocument
{
    /// <summary>
    /// Identyfikator pliku w systemie plików — pełna ścieżka pliku na dysku (Windows).
    /// Wartość <c>null</c> oznacza nowy dokument, który nie został jeszcze zapisany na dysku.
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>Nazwa pliku wyświetlana w UI (np. "notatki.md").</summary>
    public string FileName { get; set; } = "Untitled.md";

    /// <summary>Bieżąca treść Markdown dokumentu.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>True, jeśli dokument został już zapisany na dysku (ma <see cref="Identifier"/>).</summary>
    public bool IsPersisted => !string.IsNullOrEmpty(Identifier);
}
