namespace SteamGate.Tests;

/// <summary>A fresh empty folder under the system temp path, deleted on dispose.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Folder = Path.Combine(Path.GetTempPath(), "steamgate-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
    }

    public string Folder { get; }

    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    public void Dispose()
    {
        Directory.Delete(Folder, recursive: true);
    }
}
