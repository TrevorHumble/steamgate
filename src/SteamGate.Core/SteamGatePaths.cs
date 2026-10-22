namespace SteamGate.Core;

/// <summary>Where SteamGate keeps its files.</summary>
public static class SteamGatePaths
{
    /// <summary>The admin-only settings and log folder, normally C:\ProgramData\SteamGate.</summary>
    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteamGate");

    /// <summary>
    /// The one program the pipe server trusts as the helper: Program Files\SteamGate\SteamGate.Helper.exe. The launcher starts it
    /// from here and the pipe server checks a client's image path against it.
    /// </summary>
    public static string HelperPath { get; } = Path.GetFullPath(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SteamGate", "SteamGate.Helper.exe"));
}
