using System.Globalization;

namespace SteamGate.Core.Steam;

/// <summary>Where Steam keeps its files. Every other module asks here instead of building these paths.</summary>
public static class SteamLayout
{
    private const string SteamApps = "steamapps";
    private const string ManifestPrefix = "appmanifest_";
    private const string ManifestSuffix = ".acf";

    /// <summary>The folder that holds the manifests and the common folder.</summary>
    public static string SteamAppsFolder(string libraryRoot) => Path.Join(libraryRoot, SteamApps);

    /// <summary>The folder that holds one sub-folder per installed game.</summary>
    public static string CommonFolder(string libraryRoot) => Path.Join(libraryRoot, SteamApps, "common");

    /// <summary>The file that lists the extra library folders.</summary>
    public static string LibraryFoldersFile(string steamRoot) => Path.Join(steamRoot, SteamApps, "libraryfolders.vdf");

    /// <summary>The appmanifest_*.acf files in a steamapps folder. Throws IOException or a related exception when the folder cannot be read.</summary>
    public static IEnumerable<string> ManifestFiles(string steamAppsFolder) =>
        Directory.EnumerateFiles(steamAppsFolder, ManifestPrefix + "*" + ManifestSuffix)
            .Where(path => path.EndsWith(ManifestSuffix, StringComparison.OrdinalIgnoreCase));

    /// <summary>The app id in a manifest's file name, or false when the name is not appmanifest_&lt;whole number&gt;.acf. Never throws.</summary>
    public static bool TryManifestAppId(string manifestPath, out ulong appId)
    {
        var file = Path.GetFileName(manifestPath);
        appId = 0;
        if (file.Length < ManifestPrefix.Length + ManifestSuffix.Length
            || !file.StartsWith(ManifestPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ulong.TryParse(file[ManifestPrefix.Length..^ManifestSuffix.Length], NumberStyles.None, CultureInfo.InvariantCulture, out appId);
    }
}
