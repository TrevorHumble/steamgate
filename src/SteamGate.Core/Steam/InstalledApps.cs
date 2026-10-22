namespace SteamGate.Core.Steam;

/// <summary>
/// A game folder under steamapps\common and the name to show for it. MovedTo is where the folder really is when it is itself a
/// junction or symbolic link to a local folder, because Windows reports a running program's path with the link resolved.
/// </summary>
public sealed record InstalledApp(string Folder, string DisplayName, string? MovedTo = null);

/// <summary>Lists the games installed in a Steam library. The files are untrusted input, so nothing here throws.</summary>
public static class InstalledApps
{
    private const int MaxManifests = 2048;
    private const int MaxFolders = 4096;

    /// <summary>
    /// One entry per folder under steamapps\common, sorted by display name. A manifest only supplies the name:
    /// when several name the same installdir the lowest app id wins, and a folder with no manifest shows its folder name.
    /// A manifest is passed to skipped and left out when its file name holds no app id, it is malformed or has no installdir,
    /// it is oversized or unreadable (including a manifest link that cannot be followed), isLocalDrive refuses it or a link on the way to it, or it is past the budget.
    /// It reads at most 2048 manifests and 4096 game folders; those past the limits are left out without a call to skipped.
    /// A game folder that is itself a junction or symbolic link has its final path in MovedTo, but only when isLocalDrive accepts every link on the way
    /// and <see cref="LocalPathPolicy.MovedTargetAllowed(string)"/> allows the target. Every folder and file is first resolved through isLocalDrive,
    /// so a link to a network share is refused before anything on it is read.
    /// </summary>
    /// <param name="libraryRoot">The library folder that holds steamapps.</param>
    /// <param name="skipped">Called with the path of each manifest that was left out.</param>
    /// <param name="isLocalDrive">True for a path on a fixed or removable drive.</param>
    /// <param name="budget">Shared by every library of one refresh.</param>
    public static IReadOnlyList<InstalledApp> Read(
        string libraryRoot, Action<string> skipped, Func<string, bool> isLocalDrive, ManifestBudget budget) =>
        Read(libraryRoot, skipped, isLocalDrive, budget, PolicyRoots.FromEnvironment());

    /// <summary>The same read with the protected folders supplied, so a test can stand a temp folder in for a drive or the system folder.</summary>
    internal static IReadOnlyList<InstalledApp> Read(
        string libraryRoot, Action<string> skipped, Func<string, bool> isLocalDrive, ManifestBudget budget, PolicyRoots roots)
    {
        try
        {
            if (SteamPaths.FinalPath(SteamLayout.CommonFolder(libraryRoot), isLocalDrive) is not { } common)
            {
                return [];
            }

            // Two folders that differ only in case keep the first one's spelling.
            var onDiskSpelling = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var movedTo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateDirectories(common).Take(MaxFolders))
            {
                var name = Path.GetFileName(path);
                if (onDiskSpelling.TryAdd(name, name) && MovedTarget(path, isLocalDrive, roots) is { } target)
                {
                    movedTo[name] = target;
                }
            }

            var names = ReadNames(libraryRoot, onDiskSpelling, skipped, isLocalDrive, budget);
            return [.. onDiskSpelling.Values
                .Select(folder => new InstalledApp(folder, names.GetValueOrDefault(folder) ?? folder, movedTo.GetValueOrDefault(folder)))
                .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>Joins the results of several libraries in order and keeps the first entry for each folder.</summary>
    public static IReadOnlyList<InstalledApp> Merge(IEnumerable<IReadOnlyList<InstalledApp>> libraries)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. libraries.SelectMany(apps => apps).Where(app => seen.Add(app.Folder))];
    }

    // Where a game folder that is itself a junction or symbolic link really is, or null when it is not one or a target on the way is refused.
    // folderPath is a child of the resolved common folder, so it differs from its final path only when the folder itself is a link.
    // A target that is a drive root, or lies under the system folder, %ProgramData%, or a program folder (the default Steam library excepted), is refused as well.
    private static string? MovedTarget(string folderPath, Func<string, bool> isLocalDrive, PolicyRoots roots) =>
        SteamPaths.FinalPath(folderPath, isLocalDrive) is { } final
        && !string.Equals(final, folderPath, StringComparison.OrdinalIgnoreCase)
        && LocalPathPolicy.MovedTargetAllowed(final, roots) ? final : null;

    // Maps each on-disk folder named by a readable manifest to that manifest's name; the lowest app id wins.
    private static Dictionary<string, string> ReadNames(
        string libraryRoot, Dictionary<string, string> onDiskSpelling, Action<string> skipped, Func<string, bool> isLocalDrive, ManifestBudget budget)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var manifests = new List<(ulong AppId, string Path)>();
        if (SteamPaths.FinalPath(SteamLayout.SteamAppsFolder(libraryRoot), isLocalDrive) is not { } steamApps)
        {
            return names;
        }

        foreach (var path in SteamLayout.ManifestFiles(steamApps).Take(MaxManifests))
        {
            if (SteamLayout.TryManifestAppId(path, out var appId))
            {
                manifests.Add((appId, path));
            }
            else
            {
                skipped(path);
            }
        }

        foreach (var (_, path) in manifests.OrderBy(manifest => manifest.AppId))
        {
            var state = ReadAppState(path, isLocalDrive, budget);
            var installDir = state is null ? null : VdfReader.Text(state, "installdir");
            if (state is null || installDir is null)
            {
                skipped(path);
            }
            else if (onDiskSpelling.TryGetValue(installDir, out var folder) && !names.ContainsKey(folder))
            {
                var name = VdfReader.Text(state, "name");
                names[folder] = string.IsNullOrEmpty(name) ? folder : name;
            }
        }

        return names;
    }

    // The manifest is first resolved through isLocalDrive, file name included, so a link to a network share is refused before it is opened.
    // The read is limited to the smaller of the remaining budget and MaxVdfBytes, and the budget is charged the bytes actually read,
    // so a file that grows after it was looked at cannot take more than the budget holds; a file over the limit is skipped.
    private static Dictionary<string, object>? ReadAppState(string path, Func<string, bool> isLocalDrive, ManifestBudget budget)
    {
        if (SteamPaths.FinalPath(path, isLocalDrive) is not { } real)
        {
            return null;
        }

        var (text, _) = SteamFiles.ReadCapped(real, Math.Min(budget.Remaining, SteamFiles.MaxVdfBytes), out var bytesRead);
        budget.Charge(bytesRead);
        return text is not null && VdfReader.TryParse(text) is { } root ? VdfReader.Block(root, "AppState") : null;
    }
}
