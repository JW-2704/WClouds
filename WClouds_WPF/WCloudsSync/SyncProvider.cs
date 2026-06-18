using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WCloudsSync
{
    /// <summary>
    /// CF-API-basierter SyncProvider:
    /// Erstellt Platzhalter-Dateien (Cloud-Icon, kein lokaler Inhalt).
    /// Windows ruft OnFetchData auf, wenn der Nutzer eine Datei öffnet;
    /// wir laden sie dann herunter und liefern die Bytes via CfExecute.
    /// </summary>
    public class SyncProvider : IDisposable
    {
        public static string SyncRootPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WClouds");

        private static string OwnFolderPath    => Path.Combine(SyncRootPath, "Meine Dateien");
        private static string SharedFolderPath => Path.Combine(SyncRootPath, "Geteilt mit mir");

        private readonly WCloudsApiClient _api;
        private long _connectionKey;

        // Delegates müssen als Felder gehalten werden, damit der GC sie nicht einsammelt.
        private CfApi.CF_CALLBACK? _fetchDataDelegate;
        private CfApi.CF_CALLBACK? _cancelFetchDelegate;

        public SyncProvider(SessionData session)
        {
            _api = new WCloudsApiClient(session);
            EnsureFolderStructure();
        }

        public static void EnsureFolderStructure()
        {
            Directory.CreateDirectory(OwnFolderPath);
            Directory.CreateDirectory(SharedFolderPath);
        }

        // ── CF Sync Root registrieren ──────────────────────────────────────────

        private void RegisterSyncRoot()
        {
            IntPtr namePtr    = Marshal.StringToHGlobalUni("WClouds");
            IntPtr versionPtr = Marshal.StringToHGlobalUni("1.0.0");

            try
            {
                var reg = new CfApi.CF_SYNC_REGISTRATION
                {
                    StructSize             = (uint)Marshal.SizeOf<CfApi.CF_SYNC_REGISTRATION>(),
                    ProviderName           = namePtr,
                    ProviderVersion        = versionPtr,
                    ProviderId             = new Guid("A7B1C2D3-E4F5-6789-ABCD-EF0123456789"),
                    SyncRootIdentity       = IntPtr.Zero,
                    SyncRootIdentityLength = 0,
                    FileIdentity           = IntPtr.Zero,
                    FileIdentityLength     = 0,
                };

                var pol = new CfApi.CF_SYNC_POLICIES
                {
                    StructSize  = (uint)Marshal.SizeOf<CfApi.CF_SYNC_POLICIES>(),
                    Hydration   = new CfApi.CF_HYDRATION_POLICY
                    {
                        Primary  = CfApi.CF_HYDRATION_POLICY_PRIMARY.Full,
                        Modifier = CfApi.CF_HYDRATION_POLICY_MODIFIER.AutoDehydrationAllowed,
                    },
                    Population  = new CfApi.CF_POPULATION_POLICY
                    {
                        Primary  = CfApi.CF_POPULATION_POLICY_PRIMARY.Full,
                        Modifier = CfApi.CF_POPULATION_POLICY_MODIFIER.None,
                    },
                    InSync                = CfApi.CF_INSYNC_POLICY.TrackAll,
                    HardLink              = CfApi.CF_HARDLINK_POLICY.None,
                    PlaceholderManagement = CfApi.CF_PLACEHOLDER_MANAGEMENT_POLICY.Default,
                };

                int hr = CfApi.CfRegisterSyncRoot(SyncRootPath, in reg, in pol,
                    CfApi.CF_REGISTER_FLAGS.Update);

                if (!CfApi.Succeeded(hr))
                    Log($"CfRegisterSyncRoot fehlgeschlagen: 0x{hr:X8}");
                else
                    Log("CF Sync Root registriert.");
            }
            finally
            {
                Marshal.FreeHGlobal(namePtr);
                Marshal.FreeHGlobal(versionPtr);
            }
        }

        private void ConnectSyncRoot()
        {
            _fetchDataDelegate   = OnFetchData;
            _cancelFetchDelegate = static (_, _) => { };

            var table = new CfApi.CF_CALLBACK_REGISTRATION[]
            {
                new CfApi.CF_CALLBACK_REGISTRATION
                {
                    Type     = CfApi.CF_CALLBACK_TYPE.FetchData,
                    Callback = Marshal.GetFunctionPointerForDelegate(_fetchDataDelegate),
                },
                new CfApi.CF_CALLBACK_REGISTRATION
                {
                    Type     = CfApi.CF_CALLBACK_TYPE.CancelFetchData,
                    Callback = Marshal.GetFunctionPointerForDelegate(_cancelFetchDelegate),
                },
                // Ende-Markierung
                new CfApi.CF_CALLBACK_REGISTRATION
                {
                    Type     = CfApi.CF_CALLBACK_TYPE.None,
                    Callback = IntPtr.Zero,
                },
            };

            int hr = CfApi.CfConnectSyncRoot(
                SyncRootPath, table, IntPtr.Zero,
                CfApi.CF_CONNECT_FLAGS.None,
                out _connectionKey);

            if (!CfApi.Succeeded(hr))
                Log($"CfConnectSyncRoot fehlgeschlagen: 0x{hr:X8}");
            else
                Log($"CF Sync Root verbunden (key={_connectionKey}).");
        }

        // ── Platzhalter anlegen ────────────────────────────────────────────────

        private async Task CreatePlaceholdersAsync(RemoteDirectory dir, string localPath)
        {
            Directory.CreateDirectory(localPath);

            if (dir.Content.Count > 0)
                await CreateFilePlaceholdersAsync(dir.Content, localPath);

            foreach (var sub in dir.SubDirectories)
            {
                string subPath = Path.Combine(localPath, Sanitize(sub.Name));
                var subDir = await _api.GetDirectoryAsync(sub.Id);
                if (subDir != null)
                    await CreatePlaceholdersAsync(subDir, subPath);
            }
        }

        private async Task CreateFilePlaceholdersAsync(IList<RemoteFile> files, string localPath)
        {
            Directory.CreateDirectory(localPath);
            long now = DateTime.UtcNow.ToFileTimeUtc();

            var nameHandles  = new List<IntPtr>();
            var identHandles = new List<GCHandle>();

            try
            {
                var infos = new List<CfApi.CF_PLACEHOLDER_CREATE_INFO>();

                foreach (var file in files)
                {
                    string name     = Sanitize($"{file.FileName}{file.Extension}");
                    string fullPath = Path.Combine(localPath, name);

                    // Bereits hydratisierte Datei nicht als leeren Platzhalter überschreiben
                    if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
                        continue;

                    long sizeBytes = 0; // Wird nach dem ersten Öffnen vom System aktualisiert

                    // FileIdentity = fileId als 4 Bytes – wird im Callback zurückgelesen
                    byte[] idBytes = BitConverter.GetBytes(file.Id);
                    var    idHandle = GCHandle.Alloc(idBytes, GCHandleType.Pinned);
                    identHandles.Add(idHandle);

                    IntPtr namePtr = Marshal.StringToHGlobalUni(name);
                    nameHandles.Add(namePtr);

                    infos.Add(new CfApi.CF_PLACEHOLDER_CREATE_INFO
                    {
                        RelativeFileName = namePtr,
                        FsMetadata = new CfApi.CF_FS_METADATA
                        {
                            FileSize  = sizeBytes,
                            BasicInfo = new CfApi.FILE_BASIC_INFO
                            {
                                FileAttributes = 0x00000020, // FILE_ATTRIBUTE_ARCHIVE
                                CreationTime   = now,
                                LastAccessTime = now,
                                LastWriteTime  = now,
                                ChangeTime     = now,
                                Pad            = 0,
                            },
                        },
                        FileIdentity       = idHandle.AddrOfPinnedObject(),
                        FileIdentityLength = (uint)idBytes.Length,
                        Flags              = CfApi.CF_PLACEHOLDER_CREATE_FLAGS.MarkInSync,
                        Result             = 0,
                        CreateUsn          = 0,
                    });
                }

                if (infos.Count == 0) return;

                var arr = infos.ToArray();
                int hr = CfApi.CfCreatePlaceholders(
                    localPath, arr, (uint)arr.Length, 0, out uint processed);

                if (CfApi.Succeeded(hr))
                    Log($"{processed}/{arr.Length} Platzhalter angelegt in '{Path.GetFileName(localPath)}'.");
                else
                    Log($"CfCreatePlaceholders fehlgeschlagen: 0x{hr:X8}");
            }
            finally
            {
                foreach (var p in nameHandles)  Marshal.FreeHGlobal(p);
                foreach (var h in identHandles) h.Free();
            }
        }

        // ── FETCH_DATA-Callback ────────────────────────────────────────────────

        // CF_CALLBACK_INFO-Offsets (x64, Windows 11):
        //   StructSize           offset  0  (DWORD,    4 Bytes)
        //   ConnectionKey        offset  8  (LONGLONG, 8 Bytes)  ← nach 4 Pad
        //   CallbackContext      offset  16
        //   VolumeGuidName       offset  24
        //   VolumeDosName        offset  32
        //   VolumeSectorSize     offset  40 (DWORD)
        //   SyncRootFileId       offset  48 (LARGE_INTEGER)      ← nach 4 Pad
        //   SyncRootIdentity     offset  56
        //   SyncRootIdentityLen  offset  64 (DWORD)
        //   FileId               offset  72 (LARGE_INTEGER)      ← nach 4 Pad
        //   FileSize             offset  80
        //   FileIdentity         offset  88 (PVOID)
        //   FileIdentityLength   offset  96 (DWORD)
        //   NormalizedPath       offset 104 (PCWSTR)             ← nach 4 Pad
        //   TransferKey          offset 112 (LONGLONG)
        //   PriorityHint         offset 120 (UCHAR)
        //   CorrelationVector    offset 128                       ← nach 7 Pad
        //   ProcessInfo          offset 136
        //   RequestKey           offset 144 (LONGLONG)
        //
        // CF_CALLBACK_PARAMETERS FetchData-Offsets (x64):
        //   ParamSize            offset  0 (ULONG)
        //   RequiredFileOffset   offset  8 (LARGE_INTEGER)       ← nach 4 Pad
        //   RequiredLength       offset 16 (LARGE_INTEGER)
        private void OnFetchData(IntPtr cbInfoPtr, IntPtr cbParamsPtr)
        {
            int identLen = Marshal.ReadInt32(cbInfoPtr, 96);
            if (identLen < 4) return;

            IntPtr identPtr = Marshal.ReadIntPtr(cbInfoPtr, 88);
            if (identPtr == IntPtr.Zero) return;

            int  fileId        = Marshal.ReadInt32(identPtr);
            long connectionKey = Marshal.ReadInt64(cbInfoPtr, 8);
            long transferKey   = Marshal.ReadInt64(cbInfoPtr, 112);
            long requestKey    = Marshal.ReadInt64(cbInfoPtr, 144);
            long reqOffset     = Marshal.ReadInt64(cbParamsPtr, 8);
            long reqLength     = Marshal.ReadInt64(cbParamsPtr, 16);

            // Download asynchron; CfExecute aus dem Thread-Pool aufrufen.
            _ = Task.Run(async () =>
            {
                try
                {
                    Log($"FETCH Datei {fileId} (offset={reqOffset}, len={reqLength})…");
                    byte[]? data = await _api.DownloadFileAsync(fileId);

                    if (data == null)
                    {
                        ProvideData(connectionKey, transferKey, requestKey,
                            null, reqOffset, 0, failed: true);
                        return;
                    }

                    ProvideData(connectionKey, transferKey, requestKey,
                        data, 0, data.LongLength, failed: false);
                    Log($"Datei {fileId} hydratisiert ({data.Length:N0} Bytes).");
                }
                catch (Exception ex)
                {
                    Log($"FETCH Datei {fileId} fehlgeschlagen: {ex.Message}");
                    ProvideData(connectionKey, transferKey, requestKey,
                        null, reqOffset, 0, failed: true);
                }
            });
        }

        // ── CfExecute TransferData ─────────────────────────────────────────────

        private static void ProvideData(
            long connectionKey, long transferKey, long requestKey,
            byte[]? data, long offset, long length, bool failed)
        {
            var opInfo = new CfApi.CF_OPERATION_INFO
            {
                StructSize        = (uint)Marshal.SizeOf<CfApi.CF_OPERATION_INFO>(),
                Type              = CfApi.CF_OPERATION_TYPE.TransferData,
                ConnectionKey     = connectionKey,
                TransferKey       = transferKey,
                CorrelationVector = IntPtr.Zero,
                SyncStatus        = IntPtr.Zero,
                RequestKey        = requestKey,
            };

            var opParams = new CfApi.CF_OPERATION_PARAMETERS();
            opParams.ParamSize = 40; // CF_SIZE_OF_OP_PARAM(TransferData)

            GCHandle handle = default;
            try
            {
                if (failed || data == null)
                {
                    opParams.TransferData_CompletionStatus = CfApi.STATUS_UNSUCCESSFUL;
                    opParams.TransferData_Buffer           = IntPtr.Zero;
                    opParams.TransferData_Offset           = 0;
                    opParams.TransferData_Length           = 0;
                }
                else
                {
                    handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                    opParams.TransferData_CompletionStatus = CfApi.STATUS_SUCCESS;
                    opParams.TransferData_Buffer           = handle.AddrOfPinnedObject();
                    opParams.TransferData_Offset           = offset;
                    opParams.TransferData_Length           = length;
                }

                int hr = CfApi.CfExecute(in opInfo, ref opParams);
                if (!CfApi.Succeeded(hr))
                    Log($"CfExecute fehlgeschlagen: 0x{hr:X8}");
            }
            finally
            {
                if (handle.IsAllocated) handle.Free();
            }
        }

        // ── Haupt-Loop ─────────────────────────────────────────────────────────

        public async Task RunAsync(CancellationToken ct)
        {
            RegisterSyncRoot();
            ConnectSyncRoot();

            // Platzhalter beim Start anlegen
            await SyncPlaceholdersAsync();

            // Alle 5 Minuten: neu hinzugefügte Remote-Dateien als Platzhalter anlegen
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromMinutes(5), ct); }
                catch (OperationCanceledException) { break; }

                try { await SyncPlaceholdersAsync(); }
                catch (Exception ex) { Log($"Platzhalter-Refresh fehlgeschlagen: {ex.Message}"); }
            }

            Disconnect();
            Log("SyncProvider gestoppt.");
        }

        private async Task SyncPlaceholdersAsync()
        {
            Log("Aktualisiere Platzhalter…");

            var root = await _api.GetRootDirectoryAsync();
            if (root != null)
                await CreatePlaceholdersAsync(root, OwnFolderPath);

            var shared = await _api.GetSharedWithMeAsync();
            if (shared != null)
            {
                var readable = new List<RemoteFile>();
                foreach (var f in shared)
                    if (f.CanRead)
                        readable.Add(new RemoteFile
                        {
                            Id        = f.Id,
                            FileName  = f.FileName,
                            Extension = f.Extension,
                        });

                await CreateFilePlaceholdersAsync(readable, SharedFolderPath);
            }

            Log("Platzhalter aktualisiert.");
        }

        // ── Cleanup ────────────────────────────────────────────────────────────

        private void Disconnect()
        {
            if (_connectionKey != 0)
            {
                CfApi.CfDisconnectSyncRoot(_connectionKey);
                _connectionKey = 0;
            }
        }

        public void Dispose() => Disconnect();

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static void Log(string msg) =>
            Console.WriteLine($"[WCloudsSync {DateTime.Now:HH:mm:ss}] {msg}");
    }
}
