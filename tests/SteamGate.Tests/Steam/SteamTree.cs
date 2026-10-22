using System.Diagnostics;
using System.Globalization;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

/// <summary>Builds small Steam library trees inside a temp folder at test run time.</summary>
internal static class SteamTree
{
    public static string Manifest(string? name, string installDir)
    {
        var nameLine = name is null ? string.Empty : "\t\"name\"\t\t\"" + name + "\"\n";
        return "\"AppState\"\n{\n" + nameLine + "\t\"installdir\"\t\t\"" + installDir + "\"\n}\n";
    }

    public static void WriteManifest(string libraryRoot, string fileName, string text)
    {
        var steamapps = Path.Combine(libraryRoot, "steamapps");
        Directory.CreateDirectory(steamapps);
        File.WriteAllText(Path.Combine(steamapps, fileName), text);
    }

    public static void MakeGameFolder(string libraryRoot, string folder) =>
        Directory.CreateDirectory(Path.Combine(libraryRoot, "steamapps", "common", folder));

    /// <summary>The text of a libraryfolders.vdf whose entries "0", "1", ... hold these paths.</summary>
    public static string LibraryFoldersVdf(params string[] paths) =>
        "\"libraryfolders\" {" + string.Concat(paths.Select((path, i) =>
            " \"" + i.ToString(CultureInfo.InvariantCulture) + "\" { \"path\" \"" + path.Replace("\\", "\\\\", StringComparison.Ordinal) + "\" }")) + " }";

    public static void WriteVdf(string steamRoot, string text)
    {
        Directory.CreateDirectory(Path.Combine(steamRoot, "steamapps"));
        File.WriteAllText(SteamLayout.LibraryFoldersFile(steamRoot), text);
    }

    /// <summary>Makes a directory junction (no administrator needed). Remove it with Directory.Delete(link) before the temp folder goes.</summary>
    public static void MakeJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);
        using var process = Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    /// <summary>Reads a library with every parameter given openly: any path counts as local, and a fresh 16 MB budget.</summary>
    public static IReadOnlyList<InstalledApp> ReadAll(string libraryRoot, Action<string>? skipped = null, Func<string, bool>? isLocalDrive = null, ManifestBudget? budget = null) =>
        InstalledApps.Read(libraryRoot, skipped ?? (_ => { }), isLocalDrive ?? (_ => true), budget ?? new ManifestBudget());

    public static string[] Pairs(IEnumerable<InstalledApp> apps) =>
        [.. apps.Select(app => app.Folder + "|" + app.DisplayName)];
}
