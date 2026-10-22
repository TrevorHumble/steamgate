namespace SteamGate.Core.Steam;

/// <summary>A game with at least one running process. Processes carry the start time so a reused process id can be told apart.</summary>
public sealed record RunningGame(string Folder, string DisplayName, IReadOnlyList<ProcessSample> Processes);

/// <summary>Decides which running programs belong to which Steam game.</summary>
public static class GameMatcher
{
    /// <summary>
    /// A process belongs to the game whose folder is directly under steamapps\common of a library and holds its exe.
    /// The first library in order that holds it wins. Folders on the not-counted list are left out; every folder
    /// comparison ignores case. A game's folder is spelled as in installed, else as in its lowest process id's path.
    /// Windows reports a program's path with junctions and symbolic links resolved, so a process also belongs to a game
    /// when its path is under a commonTargets folder (a moved steamapps\common) or under the MovedTo path of an installed game.
    /// As a last guard a process whose path is under the system folder never matches, whatever the libraries and links say.
    /// A process whose path cannot be made absolute, or whose exe sits directly in a common folder with no game folder between, matches no game.
    /// </summary>
    /// <param name="processes">The running programs.</param>
    /// <param name="libraryRoots">The listed library folders, in order.</param>
    /// <param name="installed">The installed games.</param>
    /// <param name="notCounted">Folder names that never count.</param>
    /// <param name="commonTargets">Where the libraries' moved steamapps\common folders really are.</param>
    /// <param name="systemRoot">The system folder no game can be under; null means %SystemRoot%. Tests give their own.</param>
    public static IReadOnlyList<RunningGame> Match(
        IEnumerable<ProcessSample> processes,
        IReadOnlyList<string> libraryRoots,
        IReadOnlyCollection<InstalledApp> installed,
        IEnumerable<string> notCounted,
        IEnumerable<string>? commonTargets = null,
        string? systemRoot = null)
    {
        var system = systemRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var commonPrefixes = libraryRoots.Select(SteamLayout.CommonFolder).Concat(commonTargets ?? []).Select(WithSeparator).ToList();
        var movedGames = installed
            .Where(app => app.MovedTo is not null)
            .Select(app => (Prefix: WithSeparator(app.MovedTo!), app.Folder))
            .ToList();
        var excluded = new HashSet<string>(notCounted, StringComparer.OrdinalIgnoreCase);
        var installedByFolder = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var app in installed)
        {
            installedByFolder.TryAdd(app.Folder, app);
        }

        var membersByFolder = new Dictionary<string, List<Member>>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in processes)
        {
            var folder = FolderOf(process.ExePath, commonPrefixes, movedGames, system);
            if (folder is null || excluded.Contains(folder))
            {
                continue;
            }

            if (!membersByFolder.TryGetValue(folder, out var members))
            {
                members = [];
                membersByFolder[folder] = members;
            }

            members.Add(new Member(process, folder));
        }

        return [.. membersByFolder
            .Select(entry => ToGame(entry.Key, entry.Value, installedByFolder))
            .OrderBy(game => game.Folder, StringComparer.OrdinalIgnoreCase)];
    }

    // A process and the spelling of its game's folder as it appears in that process's own path.
    private sealed record Member(ProcessSample Process, string FolderAsWritten);

    private static RunningGame ToGame(
        string folderKey,
        List<Member> members,
        Dictionary<string, InstalledApp> installedByFolder)
    {
        var byPid = members.OrderBy(member => member.Process.Pid).ToList();
        var installedApp = installedByFolder.GetValueOrDefault(folderKey);
        var folder = installedApp?.Folder ?? byPid[0].FolderAsWritten;
        return new RunningGame(folder, installedApp?.DisplayName ?? folder, [.. byPid.Select(member => member.Process)]);
    }

    // A path that already ends in a separator (a drive root such as E:\) keeps it; any other gets one, so a folder name is never a prefix of a longer one.
    private static string WithSeparator(string path) =>
        Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    // The Steam folder name of the game this exe belongs to, or null.
    private static string? FolderOf(string exePath, List<string> commonPrefixes, List<(string Prefix, string Folder)> movedGames, string systemRoot)
    {
        string full;
        try
        {
            full = Path.GetFullPath(exePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (LocalPathPolicy.IsUnder(full, systemRoot))
        {
            return null;
        }

        foreach (var prefix in commonPrefixes)
        {
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = full[prefix.Length..];
            var separator = rest.IndexOf(Path.DirectorySeparatorChar, StringComparison.Ordinal);
            if (separator > 0)
            {
                return rest[..separator];
            }
        }

        return movedGames.FirstOrDefault(moved => full.StartsWith(moved.Prefix, StringComparison.OrdinalIgnoreCase)).Folder;
    }
}
