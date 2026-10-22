using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SteamLayoutTests
{
    [Fact]
    public void Paths_AreBuiltUnderSteamapps()
    {
        Assert.Equal(@"C:\Steam\steamapps\common", SteamLayout.CommonFolder(@"C:\Steam"));
        Assert.Equal(@"D:\steamapps\common", SteamLayout.CommonFolder(@"D:\"));
        Assert.Equal(@"C:\Steam\steamapps\libraryfolders.vdf", SteamLayout.LibraryFoldersFile(@"C:\Steam"));
    }

    [Fact]
    public void ManifestFiles_ListsOnlyAcfManifests()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "x");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf.bak", "x");
        SteamTree.WriteManifest(temp.Folder, "other.acf", "x");

        var files = SteamLayout.ManifestFiles(SteamLayout.SteamAppsFolder(temp.Folder)).Select(path => Path.GetFileName(path));

        Assert.Equal(["appmanifest_1.acf"], files);
    }

    [Theory]
    [InlineData("x.acf")]
    [InlineData("appmanifest.acf")]
    [InlineData("appmanifest_acf")]
    [InlineData("other_12.acf")]
    [InlineData("")]
    public void ShortManifestNameIsNotAnAppId(string file)
    {
        var parsed = SteamLayout.TryManifestAppId(Path.Combine(@"C:\Steam\steamapps", file), out var appId);

        Assert.False(parsed);
        Assert.Equal(0UL, appId);
    }

    [Fact]
    public void BadNameInSteamapps_DoesNotBlankTheLibrary()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "x.acf", "x");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_.acf", "x");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_3.acf", SteamTree.Manifest("Good Game", "Good"));
        SteamTree.MakeGameFolder(temp.Folder, "Good");
        var skipped = new List<string>();

        var apps = SteamTree.ReadAll(temp.Folder, skipped.Add);

        Assert.Equal(["Good|Good Game"], SteamTree.Pairs(apps));
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_.acf")], skipped);
    }

    [Theory]
    [InlineData("appmanifest_1382070.acf", true, 1382070UL)]
    [InlineData("appmanifest_0.acf", true, 0UL)]
    [InlineData("appmanifest_abc.acf", false, 0UL)]
    [InlineData("appmanifest_.acf", false, 0UL)]
    [InlineData("appmanifest_-4.acf", false, 0UL)]
    [InlineData("appmanifest_+4.acf", false, 0UL)]
    public void TryManifestAppId_ReadsWholeNumbersOnly(string file, bool ok, ulong expected)
    {
        var parsed = SteamLayout.TryManifestAppId(Path.Combine(@"C:\Steam\steamapps", file), out var appId);

        Assert.Equal((ok, expected), (parsed, appId));
    }
}
