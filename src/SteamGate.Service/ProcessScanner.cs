using System.Diagnostics;
using SteamGate.Core.Steam;

namespace SteamGate.Service;

/// <summary>
/// Lists running processes with their full exe path and start time. It opens each process with the limited-query right,
/// which still works on protected (anti-cheat) processes where Process.MainModule fails.
/// </summary>
internal static class ProcessScanner
{
    public static IReadOnlyList<ProcessSample> Scan()
    {
        var found = new List<ProcessSample>();
        var pathBuffer = new char[ProcessImage.MaxPathChars];
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
        using var handle = ProcessImage.OpenForQuery(pid);
        return !handle.IsInvalid
            && ProcessImage.TryReadPath(handle, pathBuffer, out exePath)
            && ProcessStartTime.TryRead(handle, out startUtc);
    }
}
