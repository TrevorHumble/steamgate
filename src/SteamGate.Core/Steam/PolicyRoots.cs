namespace SteamGate.Core.Steam;

/// <summary>The folders a moved game or library may not land in. Tests supply their own so a temp folder can stand in for a drive or the system folder.</summary>
/// <param name="SystemRoot">%SystemRoot%; nothing under it is ever a moved target or a game.</param>
/// <param name="ProgramFiles">%ProgramFiles%.</param>
/// <param name="ProgramFilesX86">%ProgramFiles(x86)%.</param>
/// <param name="ProgramData">%ProgramData%; nothing under it is ever a moved target, since it holds the service's own settings and tally.</param>
/// <param name="UsersRoot">The Users folder on the system drive; nothing under it is ever a moved target, since a junction there would let a standard user make the SYSTEM service end another user's programs.</param>
/// <param name="SteamCommon">The one folder under a program folder that stays allowed: the default Steam library's steamapps\common.</param>
internal sealed record PolicyRoots(string SystemRoot, string ProgramFiles, string ProgramFilesX86, string ProgramData, string UsersRoot, string SteamCommon)
{
    public static PolicyRoots FromEnvironment()
    {
        var x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new PolicyRoots(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            x86,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            UsersFolderOnSystemDrive(),
            x86.Length == 0 ? string.Empty : SteamLayout.CommonFolder(Path.Join(x86, "Steam")));
    }

    private static string UsersFolderOnSystemDrive()
    {
        var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        return string.IsNullOrEmpty(root) ? string.Empty : Path.Join(root, "Users");
    }
}
