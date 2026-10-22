using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SteamGate.Service;

/// <summary>Reads when a process started, in UTC, from an open process handle. Used by the scanner and the killer so both compare the same value.</summary>
internal static class ProcessStartTime
{
    /// <summary>False when the handle cannot give a start time. The value is the FILETIME count read as UTC; no local time is involved.</summary>
    public static bool TryRead(SafeProcessHandle handle, out DateTime startUtc)
    {
        startUtc = default;
        if (!GetProcessTimes(handle, out var created, out _, out _, out _) || created <= 0)
        {
            return false;
        }

        startUtc = DateTime.FromFileTimeUtc(created);
        return true;
    }

    // Each FILETIME is two 32-bit halves, which read as one 64-bit count of 100 ns ticks.
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
}
