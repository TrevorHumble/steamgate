using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace SteamGate.Service;

/// <summary>Opens a process and reads its full exe path. The scanner, the killer, and the pipe server all go through here.</summary>
internal static class ProcessImage
{
    // PROCESS_QUERY_LIMITED_INFORMATION: enough for the path and start time, and it still works on protected (anti-cheat) processes where Process.MainModule fails.
    private const uint QueryLimited = 0x1000;

    // PROCESS_TERMINATE.
    private const uint Terminate = 0x0001;

    /// <summary>The longest Windows path in characters; the size of a path buffer.</summary>
    internal const int MaxPathChars = 32768;

    /// <summary>A handle that can read the path and start time. Invalid when the process is gone or cannot be opened.</summary>
    public static SafeProcessHandle OpenForQuery(int pid) => OpenProcess(QueryLimited, false, (uint)pid);

    /// <summary>A handle that can read the start time and end the process. Invalid when the process is gone or cannot be opened.</summary>
    public static SafeProcessHandle OpenForTerminate(int pid) => OpenProcess(Terminate | QueryLimited, false, (uint)pid);

    /// <summary>False when the path cannot be read. The buffer is sized for the longest Windows path, so a long path is never cut short.</summary>
    public static bool TryReadPath(SafeProcessHandle handle, out string exePath) => TryReadPath(handle, new char[MaxPathChars], out exePath);

    /// <summary>The same read with a caller-owned buffer of MaxPathChars, so a scan of every process allocates it once.</summary>
    public static bool TryReadPath(SafeProcessHandle handle, char[] buffer, out string exePath)
    {
        exePath = string.Empty;
        var size = (uint)buffer.Length;
        if (!QueryFullProcessImageName(handle, 0, buffer, ref size) || size == 0)
        {
            return false;
        }

        exePath = new string(buffer, 0, (int)size);
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref uint size);
}
