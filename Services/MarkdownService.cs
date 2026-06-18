using System.Net;
using System.Text;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace WinMD.Services;

/// <summary>
/// Implementacja <see cref="IMarkdownService"/> oparta o bibliotekę Markdig.
/// Renderuje Markdown (GitHub-flavored) do kompletnego dokumentu HTML5
/// z osadzonym arkuszem stylów (light/dark) zoptymalizowanym pod
/// wyświetlanie w WebView (offline, bez zewnętrznych zasobów).
/// </summary>
public sealed class MarkdownService : IMarkdownService
{
    // Pipeline budowany raz i współdzielony między wywołaniami.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .Build();

    // Token zastępczy podmieniany na wyrenderowane HTML.
    private const string BodyPlaceholder = "%%BODY%%";

    // Token zastępczy podmieniany na zestaw zmiennych CSS motywu (jasny/ciemny).
    private const string RootPlaceholder = "%%ROOT%%";

    // Token zastępczy podmieniany na automatyczny spis treści (z nagłówków).
    private const string TocPlaceholder = "%%TOC%%";

    private const string LightRoot = @":root {
  --fg: #24292f;
  --muted: #57606a;
  --bg: #ffffff;
  --border: #d0d7de;
  --code-bg: #f6f8fa;
  --link: #0969da;
}";

    private const string DarkRoot = @":root {
  --fg: #e6edf3;
  --muted: #9da7b3;
  --bg: #1c1c1e;
  --border: #3a3a3c;
  --code-bg: #2a2a2e;
  --link: #6cb6ff;
}";

    // Szablon dokumentu HTML jako zwykły verbatim string (@"...") — nie interpolowany,
    // dzięki czemu klamry { } w CSS nie wymagają escapowania. Cudzysłowy podwajamy.
    private const string HtmlTemplate = @"<!DOCTYPE html>
<html lang=""pl"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<style>
%%ROOT%%
* { box-sizing: border-box; }
html { -webkit-text-size-adjust: 100%; }
body {
  font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif, ""Apple Color Emoji"", ""Segoe UI Emoji"";
  font-size: 16px;
  line-height: 1.6;
  color: var(--fg);
  background-color: var(--bg);
  padding: 16px;
  margin: 0 auto;
  max-width: 720px;
  word-wrap: break-word;
  overflow-wrap: break-word;
}
h1, h2, h3, h4, h5, h6 {
  margin-top: 1.2em;
  margin-bottom: 0.6em;
  line-height: 1.25;
  font-weight: 600;
}
h1 { font-size: 1.8em; padding-bottom: 0.3em; border-bottom: 1px solid var(--border); }
h2 { font-size: 1.45em; padding-bottom: 0.3em; border-bottom: 1px solid var(--border); }
h3 { font-size: 1.2em; }
h4 { font-size: 1.05em; }
h5 { font-size: 0.95em; }
h6 { font-size: 0.9em; color: var(--muted); }
p { margin-top: 0; margin-bottom: 1em; }
a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }
code {
  font-family: ""SFMono-Regular"", Consolas, ""Liberation Mono"", Menlo, monospace;
  font-size: 0.875em;
  background-color: var(--code-bg);
  padding: 0.2em 0.4em;
  border-radius: 6px;
}
pre {
  background-color: var(--code-bg);
  padding: 12px 16px;
  border-radius: 6px;
  overflow-x: auto;
  margin: 0 0 1em;
}
pre code {
  background-color: transparent;
  padding: 0;
  border-radius: 0;
  font-size: 0.875em;
}
blockquote {
  margin: 0 0 1em;
  padding: 0 1em;
  color: var(--muted);
  border-left: 0.25em solid var(--border);
}
table {
  border-collapse: collapse;
  width: 100%;
  margin: 0 0 1em;
  display: block;
  overflow-x: auto;
}
th, td {
  border: 1px solid var(--border);
  padding: 6px 13px;
}
th {
  background-color: var(--code-bg);
  font-weight: 600;
}
ul, ol {
  margin-top: 0;
  margin-bottom: 1em;
  padding-left: 2em;
}
li { margin-bottom: 0.25em; }
li.task-list-item { list-style-type: none; }
li.task-list-item > input[type=""checkbox""] {
  margin: 0 0.4em 0.15em -1.4em;
  vertical-align: middle;
}
img { max-width: 100%; height: auto; }
hr {
  height: 1px;
  border: 0;
  background-color: var(--border);
  margin: 1.5em 0;
}
.toc {
  background-color: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 10px 14px;
  margin: 0 0 1.5em;
}
.toc summary { cursor: pointer; font-weight: 600; }
.toc ul { list-style: none; margin: 0.5em 0 0; padding-left: 0; }
.toc li { margin: 0.2em 0; }
.toc a { color: var(--link); text-decoration: none; }
.toc a:hover { text-decoration: underline; }
.toc .lvl2 { padding-left: 1.2em; }
.toc .lvl3 { padding-left: 2.4em; }
h1, h2, h3, h4, h5, h6 { scroll-margin-top: 12px; }
</style>
</head>
<body>
%%TOC%%
%%BODY%%
<script>
document.addEventListener('click', function (ev) {
  var a = ev.target.closest('a[href^=""#""]');
  if (!a) return;
  var el = document.getElementById(a.getAttribute('href').slice(1));
  if (el) { ev.preventDefault(); el.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
});
</script>
</body>
</html>";

    /// <inheritdoc />
    public string ToHtml(string markdown, bool dark = false)
    {
        return HtmlTemplate
            .Replace(RootPlaceholder, dark ? DarkRoot : LightRoot)
            .Replace(TocPlaceholder, BuildToc(markdown))
            .Replace(BodyPlaceholder, ToHtmlFragment(markdown));
    }

    /// <inheritdoc />
    public string ToHtmlFragment(string markdown)
    {
        return Markdown.ToHtml(markdown ?? string.Empty, Pipeline);
    }

    /// <summary>
    /// Buduje zwijany spis treści (HTML) z nagłówków poziomu 1–3. Identyfikatory kotwic
    /// pochodzą z tego samego pipeline'u (AutoIdentifiers), więc pokrywają się z <c>id</c>
    /// nagłówków w wyrenderowanym dokumencie. Zwraca pusty string, gdy nagłówków jest &lt; 2.
    /// </summary>
    private static string BuildToc(string markdown)
    {
        var document = Markdown.Parse(markdown ?? string.Empty, Pipeline);

        var headings = new List<HeadingBlock>();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.Level <= 3 && !string.IsNullOrEmpty(heading.GetAttributes()?.Id))
                headings.Add(heading);
        }

        if (headings.Count < 2)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("<details class=\"toc\" open><summary>Contents</summary><ul>");
        foreach (var heading in headings)
        {
            var id = heading.GetAttributes()!.Id!;
            var text = WebUtility.HtmlEncode(GetHeadingText(heading));
            sb.Append("<li class=\"lvl").Append(heading.Level).Append("\">")
              .Append("<a href=\"#").Append(WebUtility.HtmlEncode(id)).Append("\">")
              .Append(text).Append("</a></li>");
        }
        sb.Append("</ul></details>");
        return sb.ToString();
    }

    /// <summary>Wyciąga czysty tekst nagłówka (litery + kod inline), bez znaczników Markdown.</summary>
    private static string GetHeadingText(HeadingBlock heading)
    {
        if (heading.Inline is null)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in heading.Inline.Descendants())
        {
            if (inline is LiteralInline literal)
                sb.Append(literal.Content.ToString());
            else if (inline is CodeInline code)
                sb.Append(code.Content);
        }
        return sb.ToString();
    }
}
