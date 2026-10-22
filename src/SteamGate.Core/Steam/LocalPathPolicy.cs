using System.Security;

namespace SteamGate.Core.Steam;

/// <summary>
/// Decides whether a path from Steam's files or the registry may be read. Those values are writable by standard users,
/// and an unreachable network path would stall the tick and make the LocalSystem account sign in to another computer.
/// </summary>
public static class LocalPathPolicy
{
    /// <summary>
    /// The full rule: the path is fully qualified, is not a network path (starts with two backslashes), is on a fixed or
    /// removable drive (Steam allows libraries on SD cards and USB drives), and so is the target of every junction or
    /// symbolic link on any part of it. Never throws.
    /// </summary>
    public static bool IsLocal(string path) =>
        Normalize(path) is { } full && DriveIsLocal(full) && SteamPaths.FinalPath(full, DriveIsLocal) is not null;

    /// <summary>
    /// The normalized path (absolute, no trailing separator) when it is fully qualified, not a network path, and accepted
    /// by isLocal; otherwise null. Never throws. isLocal sees the normalized path once.
    /// </summary>
    public static string? Usable(string path, Func<string, bool> isLocal) =>
        Normalize(path) is { } full && isLocal(full) ? full : null;

    /// <summary>
    /// False for a path that is not fully qualified or is a network path, and for a place a link must not move a game or library to: a drive root, anything under %SystemRoot% or %ProgramData%, or anything
    /// under %ProgramFiles% or %ProgramFiles(x86)% except under the fixed folder %ProgramFiles(x86)%\Steam\steamapps\common.
    /// No library and no user-written value earns an exemption, so a junction cannot make system programs count as a game.
    /// Never throws.
    /// </summary>
    /// <param name="target">The final path a link resolved to.</param>
    public static bool MovedTargetAllowed(string target) => MovedTargetAllowed(target, PolicyRoots.FromEnvironment());

    /// <summary>The same rule with the protected folders supplied, so a test can stand a temp folder in for a drive or the system folder.</summary>
    internal static bool MovedTargetAllowed(string target, PolicyRoots roots)
    {
        if (Normalize(target) is not { } full)
        {
            return false;
        }

        // Normalize keeps the separator on a drive root, so a root is as long as its own root part.
        if (Path.GetPathRoot(full)?.Length == full.Length || IsUnder(full, roots.SystemRoot) || IsUnder(full, roots.ProgramData))
        {
            return false;
        }

        return IsUnder(full, roots.SteamCommon) || !(IsUnder(full, roots.ProgramFiles) || IsUnder(full, roots.ProgramFilesX86));
    }

    /// <summary>True when path is folder itself or lies inside it, ignoring case. Both must be fully qualified; a drive root as folder holds its whole drive.</summary>
    internal static bool IsUnder(string path, string folder)
    {
        if (Normalize(path) is not { } fullPath || Normalize(folder) is not { } full)
        {
            return false;
        }

        // A drive root keeps its separator after Normalize; any other folder gets one, so C:\Win never holds C:\Windows.
        return string.Equals(fullPath, full, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(Path.EndsInDirectorySeparator(full) ? full : full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Normalize(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            return null;
        }

        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return full.StartsWith(@"\\", StringComparison.Ordinal) ? null : full;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or SecurityException)
        {
            return null;
        }
    }

    private static bool DriveIsLocal(string path)
    {
        try
        {
            return new DriveInfo(Path.GetPathRoot(path) ?? string.Empty).DriveType is DriveType.Fixed or DriveType.Removable;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
