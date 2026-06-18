using System;
using System.Threading;
using System.Threading.Tasks;

namespace WCloudsSync
{
    internal class Program
    {
        [STAThread]
        static async Task Main(string[] args)
        {
            // Explorer-Eintrag registrieren (idempotent)
            string syncRoot = SyncProvider.SyncRootPath;
            NavPaneRegistrar.Register(syncRoot);
            SyncProvider.EnsureFolderStructure();

            // Sitzung lesen (von WClouds_WPF beim Login geschrieben)
            SessionData? session = SessionStore.TryRead();
            if (session == null || session.UserId == 0)
            {
                // Noch kein Login – Prozess beenden.
                // WClouds_WPF startet ihn nach dem Login erneut.
                return;
            }

            using var cts = new CancellationTokenSource();

            // Sauberes Beenden auf Ctrl+C oder Prozess-Ende
            AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
            Console.CancelKeyPress               += (_, e) => { e.Cancel = true; cts.Cancel(); };

            using var provider = new SyncProvider(session);
            await provider.RunAsync(cts.Token);
        }
    }
}
