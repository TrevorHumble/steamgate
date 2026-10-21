namespace SteamGate.Core;

/// <summary>Where SteamGate keeps its files.</summary>
public static class SteamGatePaths
{
    /// <summary>The admin-only settings and log folder, normally C:\ProgramData\SteamGate.</summary>
    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteamGate");
}
