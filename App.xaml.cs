using Microsoft.UI.Xaml;

namespace WinMD;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    /// <summary>Współdzielony ViewModel (singleton) — źródło stanu edytora dla całej aplikacji.</summary>
    public static WinMD.ViewModels.EditorViewModel? ViewModel { get; private set; }

    /// <summary>Serwis Markdown — używany także przez okno Pomocy.</summary>
    public static WinMD.Services.IMarkdownService? Markdown { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Composition root (prosty, ręczny — bez kontenera DI).
        var settings = new WinMD.Services.JsonSettings();
        var markdown = new WinMD.Services.MarkdownService();
        Markdown = markdown;
        var files = new WinMD.Platforms.Windows.WindowsFileService();
        var pdf = new WinMD.Platforms.Windows.WindowsPdfExporter();

        var window = new MainWindow();           // MainWindow implementuje IUiService
        ViewModel = new WinMD.ViewModels.EditorViewModel(files, markdown, pdf, window, settings);
        window.Initialize(ViewModel);            // DataContext + podpięcie zdarzeń + motyw
        MainWindow = window;

        // Skojarzenie plików .md + ewentualne otwarcie pliku z argumentu wiersza poleceń.
        WinMD.Platforms.Windows.FileAssociation.EnsureRegistered();
        WinMD.Platforms.Windows.UnsavedChangesGuard.Attach(window);

        window.Activate();

        _ = TryOpenFromCommandLineAsync();
    }

    /// <summary>Otwiera plik .md przekazany jako argument (dwuklik / „Otwórz za pomocą").</summary>
    private static async System.Threading.Tasks.Task TryOpenFromCommandLineAsync()
    {
        var args = Environment.GetCommandLineArgs();
        var path = args.Skip(1).FirstOrDefault(a =>
            a.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
            a.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));
        if (path is not null && ViewModel is not null)
            await ViewModel.OpenFromExternalAsync(path);
    }
}
