namespace SteamGate.Core;

/// <summary>Decides when the service log is rolled over, and rolls it.</summary>
public static class LogRotation
{
    private const long MaxBytes = 1024 * 1024;

    /// <summary>True when the log, measured before the next line is added, is past 1 MB.</summary>
    public static bool ShouldRotate(long lengthBytes) => lengthBytes > MaxBytes;

    /// <summary>Moves the file to "path.1", replacing any older one, when it is past 1 MB. A missing file is left alone.</summary>
    public static void RollIfTooLarge(string path)
    {
        var file = new FileInfo(path);
        if (file.Exists && ShouldRotate(file.Length))
        {
            File.Move(path, path + ".1", overwrite: true);
        }
    }
}
