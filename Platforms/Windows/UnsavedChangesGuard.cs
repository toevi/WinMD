using System;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinMD.Platforms.Windows;

/// <summary>
/// Przechwytuje zamknięcie okna na Windows (przycisk „X") i — gdy są niezapisane zmiany —
/// pyta użytkownika: Zapisz / Nie zapisuj / Anuluj (natywny <see cref="ContentDialog"/>).
/// Odpowiednik androidowego <c>UnsavedChangesBackCallback</c>.
/// </summary>
internal static class UnsavedChangesGuard
{
    private static bool _forceClose;
    private static bool _prompting;

    public static void Attach(Microsoft.UI.Xaml.Window window)
    {
        var appWindow = window.AppWindow;
        if (appWindow is null)
            return;

        appWindow.Closing += async (_, args) =>
        {
            if (_forceClose || _prompting)
                return;

            var vm = ResolveViewModel();
            if (vm is null || !vm.IsDirty)
                return; // brak zmian — pozwól zamknąć

            // Wstrzymaj zamknięcie (synchronicznie, przed await) i zapytaj użytkownika.
            args.Cancel = true;

            var root = window.Content as FrameworkElement;
            if (root?.XamlRoot is null)
                return;

            _prompting = true;
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Unsaved changes",
                    Content = "Save changes before closing?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Don't save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = root.XamlRoot,
                };

                var result = await dialog.ShowAsync();

                switch (result)
                {
                    case ContentDialogResult.Primary: // Zapisz
                        if (vm.SaveCommand is IAsyncRelayCommand save)
                            await save.ExecuteAsync(null);
                        // Jeśli zapis anulowano (np. picker „Zapisz jako") — dokument wciąż brudny: zostań.
                        if (!vm.IsDirty)
                            CloseNow(window);
                        break;

                    case ContentDialogResult.Secondary: // Nie zapisuj
                        CloseNow(window);
                        break;

                    default: // Anuluj / zamknięcie dialogu — nie zamykaj
                        break;
                }
            }
            finally
            {
                _prompting = false;
            }
        };
    }

    private static void CloseNow(Microsoft.UI.Xaml.Window window)
    {
        _forceClose = true;
        window.Close();
    }

    private static WinMD.ViewModels.EditorViewModel? ResolveViewModel() => WinMD.App.ViewModel;
}
