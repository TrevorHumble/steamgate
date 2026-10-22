namespace SteamGate.Tests;

/// <summary>
/// A fresh empty folder, deleted on dispose. It is on the system drive but outside the Users folder, because a game or library
/// moved by a junction into the Users folder is refused by design, and several tests make junctions to folders made here.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!;
        Folder = Path.Combine(systemDrive, "steamgate-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
