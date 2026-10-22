using System.Runtime.InteropServices;

namespace SteamGate.Service;

/// <summary>The one place that asks Windows which session is at the console. The launcher and the pipe server both use it.</summary>
internal static class ConsoleSession
{
    private const uint NoSession = 0xFFFFFFFF;

    /// <summary>The session id of the active console session, or null when there is none (nobody is at the console).</summary>
    public static uint? ActiveId() => WTSGetActiveConsoleSessionId() is var id && id != NoSession ? id : null;

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WTSGetActiveConsoleSessionId();
}
