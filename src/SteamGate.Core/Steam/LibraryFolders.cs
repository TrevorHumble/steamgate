namespace SteamGate.Core.Steam;

/// <summary>Finds the Steam library folders from the Steam root and libraryfolders.vdf.</summary>
public static class LibraryFolders
{
    private const int MaxLibraries = 16;

    /// <summary>Returns the "path" value of each library entry in file order, or an empty list when the text is malformed.</summary>
    public static IReadOnlyList<string> Parse(string vdfText)
    {
        if (VdfReader.TryParse(vdfText) is not { } root || VdfReader.Block(root, "libraryfolders") is not { } libraries)
        {
            return [];
        }

        return [.. VdfReader.Blocks(libraries).Select(block => VdfReader.Text(block, "path")).OfType<string>()];
    }

    /// <summary>
    /// The root first, then each path from the vdf: normalized, without repeats (ignoring case), at most 16 in all.
    /// A path that is a junction or symbolic link to a library already kept counts as a repeat.
    /// The root and every path must pass <see cref="LocalPathPolicy.Usable"/> and must resolve through <see cref="SteamPaths.FinalPath(string, Func{string, bool})"/>
    /// (isLocalDrive accepts the target of every link on the way); one that fails either is dropped. Never throws.
    /// </summary>
    /// <param name="steamRoot">The Steam install folder.</param>
    /// <param name="vdfText">The text of libraryfolders.vdf, or null when it could not be read.</param>
    /// <param name="isLocalDrive">True for a fixed or removable drive; a network path would stall the service.</param>
    public static IReadOnlyList<string> Resolve(string steamRoot, string? vdfText, Func<string, bool> isLocalDrive)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in Candidates(steamRoot, vdfText))
        {
            if (result.Count >= MaxLibraries)
            {
                break;
            }

            if (LocalPathPolicy.Usable(candidate, isLocalDrive) is { } full
                && SteamPaths.FinalPath(full, isLocalDrive) is { } final
                && seen.Add(final))
            {
                result.Add(full);
            }
        }

        return result;
    }

    private static IEnumerable<string> Candidates(string steamRoot, string? vdfText)
    {
        yield return steamRoot;
        if (vdfText is not null)
        {
            foreach (var path in Parse(vdfText))
            {
                yield return path;
            }
        }
    }
}
