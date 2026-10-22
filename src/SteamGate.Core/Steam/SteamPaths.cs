namespace SteamGate.Core.Steam;

/// <summary>Resolves junctions and symbolic links the way Windows reports a running program's path. Nothing here throws.</summary>
public static class SteamPaths
{
    // Links to links to links is a loop or an attack; past this many links in one path it is refused.
    private const int MaxLinksFollowed = 8;

    /// <summary>
    /// The path with every junction and symbolic link in it resolved, from the drive root down (a link on any part of the
    /// path counts), without a trailing separator except on a drive root. A path with no link comes back as written.
    /// Links are followed one at a time: each link's stored target is read and handed to acceptTarget before anything
    /// is done with it, and a final target is never opened, so a link to a network share is refused before the network
    /// is touched. Null when a link cannot be read, loops, or is followed more than 8 times, or when acceptTarget refuses
    /// the target of any link on the way. There is no unfiltered form: every caller says what it will accept.
    /// </summary>
    /// <param name="path">A fully qualified path to a folder or a file.</param>
    /// <param name="acceptTarget">Asked about the full path each link points to before that link is followed.</param>
    public static string? FinalPath(string path, Func<string, bool> acceptTarget) =>
        FinalPath(path, acceptTarget, ReadStoredTarget);

    /// <summary>
    /// The same walk with the reading of links supplied. readLink gets a path and returns the target stored in it when
    /// it is a link, or null when it is not one; it may throw an IOException for a link it cannot read. Tests use this
    /// to stand in for a link to a network share, which cannot be made without a privilege.
    /// </summary>
    internal static string? FinalPath(string path, Func<string, bool> acceptTarget, Func<string, string?> readLink)
    {
        var linksFollowed = 0;
        return Resolve(path, acceptTarget, readLink, ref linksFollowed);
    }

    // The target stored in the link at path, or null when path is not a link. Reads the link only; the target is not opened.
    private static string? ReadStoredTarget(string path)
    {
        // FileSystemInfo.LinkTarget reads the reparse point itself whether path is a folder or a file.
        var entry = new DirectoryInfo(path);
        if (entry.LinkTarget is null)
        {
            return null;
        }

        return entry.ResolveLinkTarget(returnFinalTarget: false)?.FullName ?? throw new IOException("The link cannot be read.");
    }

    private static string? Resolve(string path, Func<string, bool> acceptTarget, Func<string, string?> readLink, ref int linksFollowed)
    {
        try
        {
            var root = Path.GetPathRoot(path) ?? string.Empty;
            var current = root;
            var parts = path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                current = Path.Join(current, part);
                if (readLink(current) is not { } stored)
                {
                    continue;
                }

                linksFollowed++;
                var targetPath = Path.TrimEndingDirectorySeparator(stored);
                if (linksFollowed > MaxLinksFollowed || !acceptTarget(targetPath))
                {
                    return null;
                }

                // The target's own path can hold links too, and so can a link that points at another link.
                if (Resolve(targetPath, acceptTarget, readLink, ref linksFollowed) is not { } resolved)
                {
                    return null;
                }

                current = resolved;
            }

            return Path.TrimEndingDirectorySeparator(current);
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return null;
        }
    }

    private static bool IsFileSystemError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
