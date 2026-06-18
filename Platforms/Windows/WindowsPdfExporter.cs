using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WinMD.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;

namespace WinMD.Platforms.Windows;

/// <summary>
/// Implementacja <see cref="IPdfExporter"/> dla Windows oparta o WebView2.
/// „Print"/„Export PDF" otwierają systemowy dialog druku (zawiera „Microsoft Print to PDF"
/// oraz drukarki fizyczne); udostępnianie PDF renderuje plik po cichu przez PrintToPdf.
/// </summary>
public sealed class WindowsPdfExporter : IPdfExporter
{
    // WebView2 musi pozostać żywy do czasu zakończenia drukowania (renderuje zadanie z DOM).
    // Trzymamy referencję i sprzątamy poprzednie zadanie przy starcie kolejnego.
    private static Popup? _printPopup;
    private static WebView2? _printWebView;

    public async Task<bool> ExportAsync(string html, string jobName)
    {
        try
        {
            var core = await PreparePrintWebViewAsync(html);
            if (core is null)
                return false;

            // Dialog systemowy: webview może zostać poza ekranem; zostawiamy go żywego do końca druku.
            core.ShowPrintUI(CoreWebView2PrintDialogKind.System);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] ExportAsync failed: {ex}");
            return false;
        }
    }

    public async Task<string?> RenderToFileAsync(string html, string fileName)
    {
        WebView2? webView = null;
        Popup? popup = null;
        try
        {
            (webView, popup) = CreateOffScreenWebView();
            var core = await LoadHtmlAsync(webView, html);
            if (core is null)
                return null;

            var path = Path.Combine(Path.GetTempPath(), fileName);
            var ok = await core.PrintToPdfAsync(path, null);
            return ok ? path : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] RenderToFileAsync failed: {ex}");
            return null;
        }
        finally
        {
            Detach(popup, webView);
        }
    }

    /// <summary>Tworzy off-screen WebView2 dla druku, sprzątając poprzedni, i ładuje HTML.</summary>
    private static async Task<CoreWebView2?> PreparePrintWebViewAsync(string html)
    {
        DetachPrint();
        var (webView, popup) = CreateOffScreenWebView();
        _printWebView = webView;
        _printPopup = popup;
        return await LoadHtmlAsync(webView, html);
    }

    private static (WebView2 webView, Popup popup) CreateOffScreenWebView()
    {
        // ~A4 @96dpi; poza ekranem (nie miga), ale w drzewie wizualnym (potrzebny HWND).
        var webView = new WebView2 { Width = 794, Height = 1123 };
        var popup = new Popup
        {
            Child = webView,
            HorizontalOffset = -100000,
            VerticalOffset = -100000,
        };

        if (WinMD.App.MainWindow?.Content is FrameworkElement root)
        {
            popup.XamlRoot = root.XamlRoot;
        }

        popup.IsOpen = true;
        return (webView, popup);
    }

    private static async Task<CoreWebView2?> LoadHtmlAsync(WebView2 webView, string html)
    {
        await webView.EnsureCoreWebView2Async();
        var core = webView.CoreWebView2;
        if (core is null)
            return null;

        var tcs = new TaskCompletionSource<bool>();
        void OnCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            core.NavigationCompleted -= OnCompleted;
            tcs.TrySetResult(e.IsSuccess);
        }

        core.NavigationCompleted += OnCompleted;
        core.NavigateToString(html);
        await tcs.Task;
        return core;
    }

    private static void DetachPrint() => Detach(_printPopup, _printWebView);

    private static void Detach(Popup? popup, WebView2? webView)
    {
        try
        {
            if (popup is not null)
                popup.IsOpen = false;
            webView?.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsPdfExporter] Detach failed: {ex}");
        }

        if (ReferenceEquals(popup, _printPopup))
            _printPopup = null;
        if (ReferenceEquals(webView, _printWebView))
            _printWebView = null;
    }
}
