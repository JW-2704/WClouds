using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WCloudsSync
{
    internal class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // ── Deinstallations-Modus ──────────────────────────────────────────
            if (args.Length > 0 && args[0] == "--unregister")
            {
                NavPaneRegistrar.Unregister();

                // Retry-Loop: Vorige WCloudsSync-Instanz braucht ggf. kurz zum Beenden
                for (int i = 0; i < 10; i++)
                {
                    int hr = CfApi.CfUnregisterSyncRoot(SyncProvider.SyncRootPath);
                    if (hr >= 0)
                    {
                        Console.WriteLine($"CfUnregisterSyncRoot: OK (Versuch {i + 1})");
                        break;
                    }
                    Console.WriteLine($"CfUnregisterSyncRoot: 0x{hr:X8} – warte 500 ms…");
                    Thread.Sleep(500);
                }
                return;
            }

            // ── Normaler Start ────────────────────────────────────────────────
            string syncRoot = SyncProvider.SyncRootPath;
            NavPaneRegistrar.Register(syncRoot);
            SyncProvider.EnsureFolderStructure();

            SessionData? session = SessionStore.TryRead();

            if (session == null || session.UserId == 0)
            {
                // Noch kein Login: Offline-Provider starten, damit Explorer
                // nicht auf Placeholder-Dateien einfriert, dann auf Login warten.
                await WaitForSessionAsync();
                return;
            }

            // Login vorhanden → normaler Sync-Betrieb
            using var cts = new CancellationTokenSource();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
            Console.CancelKeyPress               += (_, e) => { e.Cancel = true; cts.Cancel(); };

            using var provider = new SyncProvider(session);
            await provider.RunAsync(cts.Token);
        }

        /// <summary>
        /// Verbindet als Offline-Provider (Explorer-Hänger verhindern) und
        /// pollt alle 5 Sekunden auf session.json.  Sobald ein Login erkannt
        /// wird, startet sich der Prozess neu, damit RunAsync sauber aufgerufen
        /// wird.
        /// </summary>
        private static async Task WaitForSessionAsync()
        {
            SyncProvider.ConnectOffline();
            string? restartExe = null;
            try
            {
                Console.WriteLine("[WCloudsSync] Kein Login – warte auf Session…");
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    var session = SessionStore.TryRead();
                    if (session != null && session.UserId != 0)
                    {
                        restartExe = Process.GetCurrentProcess().MainModule?.FileName;
                        Console.WriteLine("[WCloudsSync] Session gefunden – starte neu.");
                        break;
                    }
                }
            }
            finally
            {
                SyncProvider.DisconnectOffline();
            }

            if (restartExe != null)
                Process.Start(restartExe);
        }
    }
}
