namespace WinMD.Services;

/// <summary>
/// Renderuje tekst Markdown do dokumentu HTML gotowego do wyświetlenia w WebView.
/// </summary>
public interface IMarkdownService
{
    /// <summary>
    /// Konwertuje Markdown (GitHub-flavored) na kompletny dokument HTML
    /// z osadzonym arkuszem stylów (CSS) zapewniającym czytelny podgląd.
    /// </summary>
    /// <param name="markdown">Treść Markdown.</param>
    /// <param name="dark">Czy użyć ciemnego motywu arkusza stylów.</param>
    /// <returns>Pełny dokument HTML jako string.</returns>
    string ToHtml(string markdown, bool dark = false);

    /// <summary>
    /// Renderuje Markdown do samego fragmentu HTML (bez opakowania &lt;html&gt;/&lt;head&gt; i CSS).
    /// Przydatne do osadzania wielu małych przykładów w jednym dokumencie.
    /// </summary>
    string ToHtmlFragment(string markdown);
}
