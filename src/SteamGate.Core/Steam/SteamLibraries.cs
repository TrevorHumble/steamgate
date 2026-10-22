namespace SteamGate.Core.Steam;

/// <summary>
/// The Steam library folders and the games installed in them, read from disk at most once a minute.
/// Steam's files are untrusted input, so nothing here throws.
/// </summary>
public sealed class SteamLibraries
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly SkipTracker _skips = new();
    private TimeSpan? _lastRead;
    private string? _root;
    private IReadOnlyList<string> _resolved = [];
    private IReadOnlyList<string> _libraries = [];
    private IReadOnlyList<string> _commonTargets = [];
    private IReadOnlyList<InstalledApp> _installed = [];

    /// <summary>
    /// Reads on the first call and again once 60 seconds have passed since the last read; otherwise returns the
    /// current lists unchanged and does not call locateRoot. A locked libraryfolders.vdf keeps the previous library
    /// list while the root is unchanged (the root alone on the first read or after the root changed).
    /// One read takes at most 16 MB of manifests in all. A library is left out when LibraryFolders.Resolve drops it (see there);
    /// when isLocalDrive refuses its steamapps or steamapps\common folder or the target of a link on the way to either;
    /// when a link on the way cannot be read, loops, or would be the ninth link followed; or when a link moved
    /// steamapps\common to a drive root, under the system folder or %ProgramData%, or under a program folder
    /// (the default Steam library excepted).
    /// </summary>
    /// <param name="locateRoot">Finds the Steam install folder; called only when a read is due.</param>
    /// <param name="sinceStart">Elapsed time from a monotonic clock, so a clock change cannot stop refreshes.</param>
    /// <param name="isLocalDrive">The local-path filter applied to each path and each link target on the way; true for a path on a fixed or removable drive.</param>
    /// <param name="logSkipped">Called with the path of each manifest that is skipped, once while it stays skipped.</param>
    /// <returns>
    /// Libraries are the listed library folders in use. CommonTargets are the final paths of those libraries' steamapps\common
    /// folders where a link moved them, because Windows reports a running program's path with links resolved.
    /// Refreshed is true when this call read from disk.
    /// </returns>
    public (IReadOnlyList<string> Libraries, IReadOnlyList<InstalledApp> Installed, IReadOnlyList<string> CommonTargets, bool Refreshed) Refresh(
        Func<string> locateRoot, TimeSpan sinceStart, Func<string, bool> isLocalDrive, Action<string> logSkipped)
    {
        if (_lastRead is { } last && sinceStart - last < Interval)
        {
            return (_libraries, _installed, _commonTargets, false);
        }

        _lastRead = sinceStart;
        var root = locateRoot();
        // The vdf is resolved through isLocalDrive first, file name included: a link to a network share is refused unread and gives the root alone.
        (string? Text, bool Locked) vdfRead = SteamPaths.FinalPath(SteamLayout.LibraryFoldersFile(root), isLocalDrive) is { } vdfPath
            ? SteamFiles.ReadCapped(vdfPath)
            : (null, false);
        if (!vdfRead.Locked || !string.Equals(root, _root, StringComparison.OrdinalIgnoreCase))
        {
            _resolved = LibraryFolders.Resolve(root, vdfRead.Text, isLocalDrive);
        }

        _root = root;
        ReadLibraries(isLocalDrive, logSkipped);
        return (_libraries, _installed, _commonTargets, true);
    }

    private void ReadLibraries(Func<string, bool> isLocalDrive, Action<string> logSkipped)
    {
        var budget = new ManifestBudget();
        var libraries = new List<string>();
        var commonTargets = new List<string>();
        var perLibrary = new List<IReadOnlyList<InstalledApp>>();
        foreach (var library in _resolved)
        {
            if (FinalCommonFolder(library, isLocalDrive) is not { } finalCommon)
            {
                continue;
            }

            libraries.Add(library);
            if (!string.Equals(finalCommon, SteamLayout.CommonFolder(library), StringComparison.OrdinalIgnoreCase))
            {
                commonTargets.Add(finalCommon);
            }

            perLibrary.Add(InstalledApps.Read(
                library,
                path =>
                {
                    if (_skips.Add(path))
                    {
                        logSkipped(path);
                    }
                },
                isLocalDrive,
                budget));
        }

        _skips.EndRefresh();
        _libraries = libraries;
        _commonTargets = commonTargets;
        _installed = InstalledApps.Merge(perLibrary);
    }

    // Where steamapps\common really is, or null when the library must not be read: isLocalDrive refuses steamapps or steamapps\common
    // or the target of a link on the way to either; a link on the way cannot be read, loops, or would be the ninth link followed; or a link
    // moved steamapps\common to a drive root, under the system folder or %ProgramData%, or under a program folder (the default Steam library excepted).
    private static string? FinalCommonFolder(string library, Func<string, bool> isLocalDrive)
    {
        var common = SteamLayout.CommonFolder(library);
        if (!isLocalDrive(SteamLayout.SteamAppsFolder(library)) || !isLocalDrive(common))
        {
            return null;
        }

        // isLocalDrive has already judged the target of every link on the way, so the final path needs no second look.
        if (SteamPaths.FinalPath(common, isLocalDrive) is not { } final)
        {
            return null;
        }

        var moved = !string.Equals(final, common, StringComparison.OrdinalIgnoreCase);
        return !moved || LocalPathPolicy.MovedTargetAllowed(final) ? final : null;
    }
}
