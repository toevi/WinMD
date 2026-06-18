using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinMD.Platforms.Windows;

/// <summary>
/// Rejestruje skojarzenie plików .md z aplikacją w gałęzi użytkownika (HKCU) — bez uprawnień
/// administratora i bez instalatora (aplikacja jest unpackaged). Idempotentne: zapisuje tylko,
/// gdy brak wpisu lub ścieżka exe się zmieniła. Po rejestracji „Otwórz za pomocą → WinMD"
/// oraz dwuklik (po jednorazowym potwierdzeniu „Zawsze") otwierają plik w aplikacji.
/// </summary>
public static class FileAssociation
{
    private const string ProgId = "WinMD.md";
    private const string Extension = ".md";
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void EnsureRegistered()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            var command = $"\"{exePath}\" \"%1\"";

            // Już zarejestrowane z tą samą komendą? Nie ruszaj rejestru.
            using (var existing = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command"))
            {
                if (existing?.GetValue(null) as string == command)
                    return;
            }

            using (var progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                progId.SetValue(null, "Markdown Document");
                using (var icon = progId.CreateSubKey("DefaultIcon"))
                    icon.SetValue(null, $"\"{exePath}\",0");
                using (var command2 = progId.CreateSubKey(@"shell\open\command"))
                    command2.SetValue(null, command);
            }

            using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
            {
                // Domyślny ProgID (fallback, gdy brak UserChoice) + obecność na liście „Otwórz za pomocą".
                ext.SetValue(null, ProgId);
                using var openWith = ext.CreateSubKey("OpenWithProgids");
                openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            // Powiadom powłokę o zmianie skojarzeń.
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileAssociation] EnsureRegistered failed: {ex}");
        }
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
