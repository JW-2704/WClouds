using System;
using System.Runtime.InteropServices;

namespace WCloudsSync
{
    /// <summary>
    /// P/Invoke-Deklarationen für die Windows Cloud Files API (cldapi.dll).
    /// Ermöglicht Platzhalter-Dateien mit Wolken-Icons wie OneDrive.
    /// </summary>
    internal static class CfApi
    {
        internal const int STATUS_SUCCESS     = 0;
        internal const int STATUS_UNSUCCESSFUL = unchecked((int)0xC0000001);
        internal static bool Succeeded(int hr) => hr >= 0;

        // ── Enums ─────────────────────────────────────────────────────────────

        internal enum CF_HYDRATION_POLICY_PRIMARY : ushort
        {
            Partial = 0, Progressive = 1, Full = 2, AlwaysFull = 3,
        }
        [Flags] internal enum CF_HYDRATION_POLICY_MODIFIER : ushort
        {
            None = 0, ValidationRequired = 1, StreamingAllowed = 2, AutoDehydrationAllowed = 4,
        }
        internal enum CF_POPULATION_POLICY_PRIMARY : ushort
        {
            Partial = 0, Full = 2, AlwaysFull = 3,
        }
        [Flags] internal enum CF_POPULATION_POLICY_MODIFIER : ushort { None = 0 }
        [Flags] internal enum CF_INSYNC_POLICY : uint   { None = 0, TrackAll = 0x005500FF }
        [Flags] internal enum CF_HARDLINK_POLICY : uint { None = 0, Allowed  = 1 }
        [Flags] internal enum CF_PLACEHOLDER_MANAGEMENT_POLICY : uint
        {
            Default = 0, CreateUnrestricted = 1, ConvertUnrestricted = 2, UpdateUnrestricted = 4,
        }
        [Flags] internal enum CF_REGISTER_FLAGS  : uint { None = 0, Update = 1 }
        [Flags] internal enum CF_CONNECT_FLAGS   : uint { None = 0 }

        internal enum CF_CALLBACK_TYPE : int
        {
            FetchData = 0, ValidateData = 1, CancelFetchData = 2, FetchPlaceholders = 3,
            OpenCompletion = 4, CloseCompletion = 5, Dehydrate = 6, DehydrateCompletion = 7,
            Delete = 8, DeleteCompletion = 9, Rename = 10, RenameCompletion = 11, None = -1,
        }
        [Flags] internal enum CF_PLACEHOLDER_CREATE_FLAGS : uint
        {
            None = 0, DisableOnDemandPopulation = 1, MarkInSync = 2, Supersede = 4, AlwaysFull = 8,
        }
        internal enum CF_OPERATION_TYPE : uint
        {
            TransferData = 0, RetrieveData = 1, AckData = 2, RestartHydration = 3,
            TransferPlaceholders = 4, AckDehydrate = 5, AckDelete = 6, AckRename = 7,
        }

        // ── Structs ───────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_HYDRATION_POLICY
        {
            internal CF_HYDRATION_POLICY_PRIMARY  Primary;
            internal CF_HYDRATION_POLICY_MODIFIER Modifier;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_POPULATION_POLICY
        {
            internal CF_POPULATION_POLICY_PRIMARY  Primary;
            internal CF_POPULATION_POLICY_MODIFIER Modifier;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_SYNC_POLICIES
        {
            internal uint StructSize;
            internal CF_HYDRATION_POLICY  Hydration;
            internal CF_POPULATION_POLICY Population;
            internal CF_INSYNC_POLICY     InSync;
            internal CF_HARDLINK_POLICY   HardLink;
            internal CF_PLACEHOLDER_MANAGEMENT_POLICY PlaceholderManagement;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_SYNC_REGISTRATION
        {
            internal uint   StructSize;
            internal IntPtr ProviderName;
            internal IntPtr ProviderVersion;
            internal IntPtr SyncRootIdentity;
            internal uint   SyncRootIdentityLength;
            internal IntPtr FileIdentity;
            internal uint   FileIdentityLength;
            internal Guid   ProviderId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_BASIC_INFO
        {
            internal long CreationTime;
            internal long LastAccessTime;
            internal long LastWriteTime;
            internal long ChangeTime;
            internal uint FileAttributes;
            internal uint Pad;  // explizit damit das Layout 100% passt
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_FS_METADATA
        {
            internal FILE_BASIC_INFO BasicInfo;  // 48 bytes (mit Pad)
            internal long            FileSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_PLACEHOLDER_CREATE_INFO
        {
            internal IntPtr RelativeFileName;
            internal CF_FS_METADATA FsMetadata;
            internal IntPtr FileIdentity;
            internal uint   FileIdentityLength;
            internal CF_PLACEHOLDER_CREATE_FLAGS Flags;
            internal int    Result;
            internal long   CreateUsn;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_CALLBACK_REGISTRATION
        {
            internal CF_CALLBACK_TYPE Type;
            internal IntPtr           Callback;
        }

        // CF_OPERATION_INFO (48 Bytes auf x64)
        [StructLayout(LayoutKind.Sequential)]
        internal struct CF_OPERATION_INFO
        {
            internal uint              StructSize;
            internal CF_OPERATION_TYPE Type;
            internal long              ConnectionKey;
            internal long              TransferKey;
            internal IntPtr            CorrelationVector;
            internal IntPtr            SyncStatus;
            internal long              RequestKey;
        }

        // CF_OPERATION_PARAMETERS union – nur TransferData-Variante
        // Layout (x64):
        //   [0]  uint  ParamSize
        //   [8]  int   CompletionStatus   (nach 4 Pad)
        //   [16] IntPtr Buffer             (nach 4 Pad für 8-Byte-Alignment)
        //   [24] long  Offset
        //   [32] long  Length
        [StructLayout(LayoutKind.Explicit, Size = 128)]
        internal struct CF_OPERATION_PARAMETERS
        {
            [FieldOffset(0)]  internal uint   ParamSize;
            [FieldOffset(8)]  internal int    TransferData_CompletionStatus;
            [FieldOffset(16)] internal IntPtr TransferData_Buffer;
            [FieldOffset(24)] internal long   TransferData_Offset;
            [FieldOffset(32)] internal long   TransferData_Length;
        }

        // ── Delegate ──────────────────────────────────────────────────────────

        // IntPtr-Variante ist robuster bei unmanaged-to-managed Callbacks
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void CF_CALLBACK(IntPtr callbackInfo, IntPtr callbackParameters);

        // ── DllImports ────────────────────────────────────────────────────────

        [DllImport("cldapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int CfRegisterSyncRoot(
            string syncRootPath,
            in CF_SYNC_REGISTRATION registration,
            in CF_SYNC_POLICIES     policies,
            CF_REGISTER_FLAGS       registerFlags);

        [DllImport("cldapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int CfConnectSyncRoot(
            string syncRootPath,
            [In] CF_CALLBACK_REGISTRATION[] callbackTable,
            IntPtr callbackContext,
            CF_CONNECT_FLAGS connectFlags,
            out long connectionKey);

        [DllImport("cldapi.dll")]
        internal static extern int CfDisconnectSyncRoot(long connectionKey);

        [DllImport("cldapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int CfCreatePlaceholders(
            string baseDirectoryPath,
            [In, Out] CF_PLACEHOLDER_CREATE_INFO[] placeholderArray,
            uint    placeholderCount,
            uint    createFlags,
            out uint entriesProcessed);

        [DllImport("cldapi.dll")]
        internal static extern int CfExecute(
            in CF_OPERATION_INFO          opInfo,
            ref CF_OPERATION_PARAMETERS   opParams);

        [DllImport("cldapi.dll", CharSet = CharSet.Unicode)]
        internal static extern int CfUnregisterSyncRoot(string syncRootPath);
    }
}
