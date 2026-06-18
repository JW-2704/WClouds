using Microsoft.Win32;
using System;
using System.IO;

namespace WCloudsSync
{
    /// <summary>
    /// Registriert WClouds als Eintrag in der linken Navigationsleiste des
    /// Windows Explorers (wie OneDrive).  Nur Registry – kein COM, kein P/Invoke.
    /// </summary>
    public static class NavPaneRegistrar
    {
        // Stabile GUID für WClouds – darf sich nie ändern
        private const string ClsidGuid = "{A7B1C2D3-E4F5-6789-ABCD-EF0123456789}";

        private static string AppExePath
        {
            get
            {
                // Installed: WClouds_WPF.exe and WCloudsSync.exe are in the same folder.
                string local = Path.Combine(AppContext.BaseDirectory, "WClouds_WPF.exe");
                if (File.Exists(local)) return local;

                // Dev build: WCloudsSync is in WCloudsSync\bin\..., WPF is in WClouds_WPF\bin\...
                // Walk up to the solution root and find the sibling WPF output.
                string dir = AppContext.BaseDirectory;
                for (int i = 0; i < 6; i++)
                {
                    dir = Path.GetDirectoryName(dir) ?? dir;
                    string candidate = Path.Combine(dir, "WClouds_WPF", "bin", "Release",
                        "net10.0-windows7.0", "WClouds_WPF.exe");
                    if (File.Exists(candidate)) return candidate;
                    candidate = Path.Combine(dir, "WClouds_WPF", "bin", "Debug",
                        "net10.0-windows7.0", "WClouds_WPF.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                return local;
            }
        }

        /// <summary>
        /// Registriert die Namespace-Erweiterung für den aktuellen Benutzer.
        /// Kann mehrfach aufgerufen werden – ist idempotent.
        /// </summary>
        public static void Register(string syncRootPath)
        {
            Directory.CreateDirectory(syncRootPath);

            // ── CLSID → ShellFolder das auf syncRootPath zeigt ───────────────
            string clsidBase = $@"Software\Classes\CLSID\{ClsidGuid}";

            using (var key = Registry.CurrentUser.CreateSubKey(clsidBase))
            {
                key.SetValue(null, "WClouds");
                key.SetValue("System.IsPinnedToNameSpaceTree", 1, RegistryValueKind.DWord);
                key.SetValue("SortOrderIndex", 87, RegistryValueKind.DWord);
            }

            // Standard-Icon aus WClouds_WPF.exe (Index 0)
            using (var iconKey = Registry.CurrentUser.CreateSubKey($@"{clsidBase}\DefaultIcon"))
                iconKey.SetValue(null, $@"{AppExePath},0");

            // InProcServer32 → shell32.dll übernimmt die Darstellung
            using (var inProc = Registry.CurrentUser.CreateSubKey($@"{clsidBase}\InProcServer32"))
            {
                inProc.SetValue(null, @"%SystemRoot%\system32\shell32.dll");
                inProc.SetValue("ThreadingModel", "Apartment");
            }

            // ShellFolder-Instanz
            using (var inst = Registry.CurrentUser.CreateSubKey($@"{clsidBase}\Instance"))
            {
                // CLSID für "Folder" (Shell-Namespace-Ordner)
                inst.SetValue("CLSID", "{0E5AAE11-A475-4c5b-AB00-C66DE400274E}");

                using var initBag = inst.CreateSubKey("InitPropertyBag");
                initBag.SetValue("Attributes", 0x11, RegistryValueKind.DWord); // READONLY | DIRECTORY
                initBag.SetValue("TargetFolderPath", syncRootPath);
            }

            // ShellFolder-Flags
            using (var sf = Registry.CurrentUser.CreateSubKey($@"{clsidBase}\ShellFolder"))
            {
                sf.SetValue("Attributes", unchecked((int)0xF080004D), RegistryValueKind.DWord);
                sf.SetValue("FolderValueFlags", 0x28, RegistryValueKind.DWord);
            }

            // ── Eintrag in Desktop\NameSpace → erscheint im Explorer-Baum ───
            string nsKey = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{ClsidGuid}";
            using (var ns = Registry.CurrentUser.CreateSubKey(nsKey))
                ns.SetValue(null, "WClouds");

            // ── HideDesktopIcons → nicht auf dem Desktop anzeigen ────────────
            string hideKey = $@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
            using (var hide = Registry.CurrentUser.CreateSubKey(hideKey))
                hide.SetValue(ClsidGuid, 1, RegistryValueKind.DWord);
        }

        /// <summary>
        /// Entfernt alle Registry-Einträge (für Deinstallation).
        /// </summary>
        public static void Unregister()
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Classes\CLSID\{ClsidGuid}", throwOnMissingSubKey: false);

            Registry.CurrentUser.DeleteSubKeyTree(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{ClsidGuid}",
                throwOnMissingSubKey: false);

            using var hide = Registry.CurrentUser.OpenSubKey(
                $@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", writable: true);
            hide?.DeleteValue(ClsidGuid, throwOnMissingValue: false);
        }
    }
}
