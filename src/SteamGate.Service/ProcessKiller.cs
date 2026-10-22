using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SteamGate.Service;

/// <summary>Ends one process, but only if it is still the process the scan saw: a reused process id has a different start time.</summary>
internal static class ProcessKiller
{
    private const uint TerminateExitCode = 1;

    /// <summary>
    /// True when the process with this id and exactly this UTC start time was found and Windows accepted the termination.
    /// False when it is gone, has another start time, cannot be opened, or refuses to end (a protected process); the caller retries.
    /// </summary>
    public static bool Kill(int pid, DateTime startUtc)
    {
        if (pid <= 0)
        {
            return false;
        }

        using var handle = ProcessImage.OpenForTerminate(pid);
        if (handle.IsInvalid)
        {
            return false;
        }

        if (!ProcessStartTime.TryRead(handle, out var actual) || actual != startUtc)
        {
            return false;
        }

        return TerminateProcess(handle, TerminateExitCode);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(SafeProcessHandle process, uint exitCode);
}
