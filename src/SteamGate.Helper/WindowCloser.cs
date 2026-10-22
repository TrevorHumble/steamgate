using System.Runtime.InteropServices;

namespace SteamGate.Helper;

/// <summary>Asks a game to close by posting WM_CLOSE to every top-level window of its processes. A window of an elevated game can refuse the post; the service force-closes anyway.</summary>
internal static class WindowCloser
{
    private const uint WmClose = 0x0010;

    public static void PostClose(IReadOnlyCollection<int> processIds)
    {
        var wanted = new HashSet<uint>(processIds.Select(id => (uint)id));
        _ = EnumWindows(
            (window, unused) =>
            {
                _ = GetWindowThreadProcessId(window, out var owner);
                if (wanted.Contains(owner))
                {
                    _ = PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
                }

                return true;
            },
            IntPtr.Zero);
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
