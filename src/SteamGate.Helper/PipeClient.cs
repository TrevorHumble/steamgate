using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using SteamGate.Core.Pipe;

namespace SteamGate.Helper;

/// <summary>
/// Opens \\.\pipe\SteamGate with exactly the rights the pipe grants a standard user (read, write, read attributes,
/// read extended attributes, read permissions, synchronize), never generic read or write, and refuses a pipe that
/// a standard user's process created. The status screen compiles this same file; the test probe opens the pipe the same way but only records the owner.
/// </summary>
internal static class PipeClient
{
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    // SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION: the server may learn who we are but may not act as us.
    private const uint SecurityQosIdentification = 0x00100000 | 0x00010000;
    private const uint OwnerSecurityInformation = 1;
    private const int KernelObject = 6;

    private static readonly SecurityIdentifier LocalSystem = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier Administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);

    /// <summary>The connected stream, or null when the pipe is missing, busy, or not owned by the system or the administrators.</summary>
    public static NamedPipeClientStream? TryConnect()
    {
        using var handle = CreateFile(PipeContract.PipePath, PipeContract.ClientAccessMask, 0, IntPtr.Zero, OpenExisting, FileFlagOverlapped | SecurityQosIdentification, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            return null;
        }

        if (!OwnerIsTrusted(handle))
        {
            return null;
        }

        // The stream takes over the handle; this scope must not close it.
        var owned = new SafePipeHandle(handle.DangerousGetHandle(), true);
        handle.SetHandleAsInvalid();
        return new NamedPipeClientStream(PipeDirection.InOut, true, true, owned);
    }

    private static bool OwnerIsTrusted(SafeFileHandle handle)
    {
        var owner = IntPtr.Zero;
        var descriptor = IntPtr.Zero;
        try
        {
            if (GetSecurityInfo(handle, KernelObject, OwnerSecurityInformation, out owner, out _, out _, out _, out descriptor) != 0
                || owner == IntPtr.Zero)
            {
                return false;
            }

            var sid = new SecurityIdentifier(owner);
            return sid.Equals(LocalSystem) || sid.Equals(Administrators);
        }
        finally
        {
            if (descriptor != IntPtr.Zero)
            {
                LocalFree(descriptor);
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetSecurityInfo(
        SafeFileHandle handle,
        int objectType,
        uint securityInfo,
        out IntPtr owner,
        out IntPtr group,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
