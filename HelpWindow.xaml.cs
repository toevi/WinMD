using System.Net;
using System.Text;
using Microsoft.UI.Xaml;
using WinMD.Models;

namespace WinMD;

public sealed partial class HelpWindow : Window
{
    private readonly bool _dark;

    public HelpWindow(bool dark)
    {
        InitializeComponent();
        AppWindow.SetIcon(System.IO.Path.Combine(
            System.AppContext.BaseDirectory, "icon.ico"));
        _dark = dark;
        Loaded();
    }

    private async void Loaded()
    {
        await HelpWeb.EnsureCoreWebView2Async();
        HelpWeb.CoreWebView2?.NavigateToString(BuildCheatsheetHtml(_dark));
    }

    private static IReadOnlyList<MarkdownTip> GetTips()
    {
        const string svg = "<svg xmlns='http://www.w3.org/2000/svg' width='140' height='64'>" +
                           "<rect width='140' height='64' rx='8' fill='#512BD4'/>" +
                           "<text x='70' y='38' font-size='15' fill='white' text-anchor='middle' " +
                           "font-family='sans-serif'>image</text></svg>";
        string imageUri = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));

        return new List<MarkdownTip>
        {
            new("Bold", "**text**", "Makes the wrapped text bold.", "**bold text**"),
            new("Italic", "*text*", "Makes the wrapped text italic.", "*italic text*"),
            new("Inline code", "`code`", "Formats text as inline code (monospace).", "Press the `Save` button."),
            new("Link", "[text](https://example.com)", "Creates a clickable hyperlink.", "[Anthropic](https://www.anthropic.com)"),
            new("Heading 1", "# Title", "Largest, top-level heading.", "# Heading 1"),
            new("Heading 2", "## Title", "Second-level section heading.", "## Heading 2"),
            new("Bullet list", "- item", "An unordered (bulleted) list.", "- First item\n- Second item"),
            new("Numbered list", "1. item", "An ordered (numbered) list.", "1. First step\n2. Second step"),
            new("Quote", "> text", "A block quote, indented with a left bar.", "> This is a quote."),
            new("Code block", "```\ncode\n```", "A multi-line, fenced code block.", "```\nvar x = 42;\n```"),
            new("Horizontal rule", "---", "A horizontal divider line.", "Above\n\n---\n\nBelow"),
            new("Image", "![alt](image.png)", "Embeds an image by path or URL.", $"![image]({imageUri})"),
        };
    }

    private string BuildCheatsheetHtml(bool dark)
    {
        var md = App.Markdown;
        var sb = new StringBuilder();
        sb.Append(BuildHead(dark));
        sb.Append("<div class=\"intro\">Click the toolbar buttons to insert any of these, or type them by hand. Each card shows the syntax and a live example of the result.</div>");

        sb.Append(BuildShortcutsHtml());

        foreach (var tip in GetTips())
        {
            sb.Append("<div class=\"card\">");
            sb.Append("<div class=\"title\">").Append(WebUtility.HtmlEncode(tip.Title)).Append("</div>");
            sb.Append("<pre class=\"syntax\">").Append(WebUtility.HtmlEncode(tip.Syntax)).Append("</pre>");
            sb.Append("<div class=\"desc\">").Append(WebUtility.HtmlEncode(tip.Description)).Append("</div>");
            sb.Append("<div class=\"example-label\">Example</div>");
            sb.Append("<div class=\"example\">").Append(md?.ToHtmlFragment(tip.Example) ?? string.Empty).Append("</div>");
            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string BuildShortcutsHtml()
    {
        var rows = new (string Keys, string Desc)[]
        {
            ("Ctrl + N", "New file"),
            ("Ctrl + O", "Open"),
            ("Ctrl + S", "Save"),
            ("Ctrl + P", "Export / Print PDF"),
            ("Ctrl + B", "Bold"),
            ("Ctrl + I", "Italic"),
            ("Ctrl + E", "Toggle Edit / Preview"),
            ("Ctrl + F", "Find (in editor or preview)"),
            ("Ctrl + H", "Replace"),
            ("F3  /  Shift + F3", "Next / previous match"),
            ("Esc", "Close the find bar"),
            ("F1", "This help"),
        };

        var sb = new StringBuilder();
        sb.Append("<style>");
        sb.Append(".sc-card kbd{display:inline-block;background:var(--syntax-bg);color:var(--syntax-fg);border:1px solid var(--border);border-radius:6px;padding:2px 8px;font-family:\"SFMono-Regular\",Consolas,Menlo,monospace;font-size:12px;white-space:nowrap;}");
        sb.Append(".sc-row{display:flex;justify-content:space-between;align-items:center;gap:14px;padding:7px 0;border-top:1px solid var(--border);}");
        sb.Append(".sc-row:first-of-type{border-top:0;}");
        sb.Append(".sc-desc{color:var(--muted);font-size:14px;}");
        sb.Append("</style>");
        sb.Append("<div class=\"card sc-card\">");
        sb.Append("<div class=\"title\">Keyboard shortcuts</div>");
        foreach (var (keys, desc) in rows)
        {
            sb.Append("<div class=\"sc-row\"><span class=\"sc-desc\">")
              .Append(WebUtility.HtmlEncode(desc))
              .Append("</span><kbd>")
              .Append(WebUtility.HtmlEncode(keys))
              .Append("</kbd></div>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string BuildHead(bool dark)
    {
        string root = dark
            ? @":root{--bg:#121212;--card:#1C1C1E;--border:#3A3A3C;--fg:#e6edf3;--muted:#9da7b3;--title:#e6edf3;--syntax-bg:#2C2640;--syntax-fg:#c6b8ff;--label:#8a8a8e;--code-bg:#2a2a2e;--link:#6cb6ff;}"
            : @":root{--bg:#F2F1F8;--card:#ffffff;--border:#E4E1F0;--fg:#24292f;--muted:#57606a;--title:#212121;--syntax-bg:#EEEAFB;--syntax-fg:#2B0B98;--label:#919191;--code-bg:#f6f8fa;--link:#512BD4;}";

        return @"<!DOCTYPE html>
<html lang=""pl"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<style>
" + root + @"
* { box-sizing: border-box; }
body {
  font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
  background: var(--bg);
  color: var(--fg);
  padding: 14px;
  margin: 0;
}
.intro { color: var(--muted); font-size: 13px; margin: 2px 2px 14px; line-height: 1.5; }
.card {
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 14px;
  margin-bottom: 12px;
}
.title { font-weight: 700; font-size: 16px; color: var(--title); margin-bottom: 8px; }
.syntax {
  background: var(--syntax-bg);
  color: var(--syntax-fg);
  font-family: ""SFMono-Regular"", Consolas, Menlo, monospace;
  font-size: 13px;
  padding: 8px 10px;
  border-radius: 8px;
  margin: 0 0 10px;
  white-space: pre-wrap;
  overflow-x: auto;
}
.desc { color: var(--muted); font-size: 14px; line-height: 1.5; margin-bottom: 10px; }
.example-label {
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: 11px;
  color: var(--label);
  border-top: 1px solid var(--border);
  padding-top: 10px;
  margin-bottom: 6px;
}
.example { font-size: 15px; line-height: 1.5; color: var(--fg); }
.example > *:first-child { margin-top: 0; }
.example > *:last-child { margin-bottom: 0; }
.example h1 { font-size: 1.5em; border-bottom: 1px solid var(--border); padding-bottom: 0.2em; margin: 0.2em 0; }
.example h2 { font-size: 1.25em; border-bottom: 1px solid var(--border); padding-bottom: 0.2em; margin: 0.2em 0; }
.example p { margin: 0.2em 0; }
.example a { color: var(--link); }
.example code {
  background: var(--code-bg);
  padding: 0.15em 0.35em;
  border-radius: 5px;
  font-family: ""SFMono-Regular"", Consolas, Menlo, monospace;
  font-size: 0.9em;
}
.example pre { background: var(--code-bg); padding: 10px 12px; border-radius: 8px; overflow-x: auto; margin: 0; }
.example pre code { background: transparent; padding: 0; }
.example blockquote { margin: 0; padding: 0 0.9em; color: var(--muted); border-left: 0.25em solid var(--border); }
.example ul, .example ol { padding-left: 1.4em; margin: 0; }
.example li { margin: 0.15em 0; }
.example img { max-width: 100%; height: auto; border-radius: 6px; }
.example hr { border: 0; height: 1px; background: var(--border); margin: 0.8em 0; }
</style>
</head>
<body>";
    }
}
