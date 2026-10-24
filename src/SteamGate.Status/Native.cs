using System.Runtime.InteropServices;

namespace SteamGate.Status;

internal static class Native
{
    // DWMWA_USE_IMMERSIVE_DARK_MODE, as the Windows Forms library itself declares it.
    private const int UseImmersiveDarkMode = 20;

    /// <summary>Switches the window's title bar to Windows' dark form, or back. A Windows that does not know the setting ignores it.</summary>
    public static void SetDarkTitleBar(IntPtr window, bool dark)
    {
        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(window, UseImmersiveDarkMode, ref value, sizeof(int));
    }

    /// <summary>
    /// Gives a window's scroll bars (and a list's) Windows' dark form, the theme name the Windows Forms library itself uses for dark mode.
    /// A Windows that does not know the theme leaves them as they were.
    /// </summary>
    public static void UseDarkScrollBars(IntPtr window) => _ = SetWindowTheme(window, "DarkMode_Explorer", null);

    [DllImport("dwmapi.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int SetWindowTheme(IntPtr window, string subAppName, string? subIdList);
}
