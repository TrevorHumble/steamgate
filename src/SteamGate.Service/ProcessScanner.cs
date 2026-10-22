using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SteamGate.Core.Steam;

namespace SteamGate.Service;

/// <summary>
/// Lists running processes with their full exe path and start time. It opens each process with the limited-query right,
/// which still works on protected (anti-cheat) processes where Process.MainModule fails.
/// </summary>
internal static class ProcessScanner
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int MaxPathChars = 32768;

    public static IReadOnlyList<ProcessSample> Scan()
    {
        var found = new List<ProcessSample>();
        var pathBuffer = new char[MaxPathChars];
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (TryDescribe(process.Id, pathBuffer, out var startUtc, out var exePath))
                {
                    found.Add(new ProcessSample(process.Id, startUtc, exePath));
                }
            }
        }

        return found;
    }

    // A process whose handle, path, or start time cannot be read is skipped.
    private static bool TryDescribe(int pid, char[] pathBuffer, out DateTime startUtc, out string exePath)
    {
        startUtc = default;
        exePath = string.Empty;
        using var handle = OpenProcess(ProcessQueryLimitedInformation, false, (uint)pid);
        if (handle.IsInvalid)
        {
            return false;
        }

        var size = (uint)pathBuffer.Length;
        if (!QueryFullProcessImageName(handle, 0, pathBuffer, ref size) || size == 0)
        {
            return false;
        }

        if (!GetProcessTimes(handle, out var created, out _, out _, out _) || created <= 0)
        {
            return false;
        }

        startUtc = DateTime.FromFileTimeUtc(created);
        exePath = new string(pathBuffer, 0, (int)size);
        return true;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] exeName, ref uint size);

    // Each FILETIME is two 32-bit halves, which read as one 64-bit count of 100 ns ticks.
    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
}
