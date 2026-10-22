using System.Globalization;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class LibraryFoldersTests
{
    private static bool CandD(string path) =>
        path.StartsWith("C:", StringComparison.OrdinalIgnoreCase) || path.StartsWith("D:", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Parse_TwoLibraries()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "libraryfolders.vdf"));

        var paths = LibraryFolders.Parse(text);

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], paths);
    }

    [Fact]
    public void Parse_Truncated_ReturnsEmpty()
    {
        Assert.Empty(LibraryFolders.Parse("\"libraryfolders\" {"));
    }

    [Fact]
    public void Parse_NoLibraryFoldersBlock_ReturnsEmpty()
    {
        Assert.Empty(LibraryFolders.Parse("\"other\" { \"0\" { \"path\" \"C:\\\\x\" } }"));
    }

    [Fact]
    public void Parse_SkipsEntriesWithoutPath()
    {
        var text = "\"libraryfolders\" { \"0\" { \"label\" \"x\" } \"1\" \"loose\" \"2\" { \"path\" \"D:\\\\Lib\" } }";

        Assert.Equal([@"D:\Lib"], LibraryFolders.Parse(text));
    }

    [Fact]
    public void Resolve_DedupesAndFallsBack()
    {
        var withVdf = LibraryFolders.Resolve(@"C:\Steam\", SteamTree.LibraryFoldersVdf(@"C:\Steam", @"D:\Lib\"), CandD);
        var withoutVdf = LibraryFolders.Resolve(@"C:\Steam", null, CandD);
        var repeatsIgnoringCase = LibraryFolders.Resolve(@"C:\Steam", SteamTree.LibraryFoldersVdf(@"c:\STEAM", @"D:\Lib", @"d:\lib"), CandD);

        Assert.Equal([@"C:\Steam", @"D:\Lib"], withVdf);
        Assert.Equal([@"C:\Steam"], withoutVdf);
        Assert.Equal([@"C:\Steam", @"D:\Lib"], repeatsIgnoringCase);
    }

    [Fact]
    public void Resolve_DriveRoot_KeepsItsSeparator()
    {
        Assert.Equal([@"C:\Steam", @"D:\"], LibraryFolders.Resolve(@"C:\Steam", SteamTree.LibraryFoldersVdf(@"D:\"), CandD));
    }

    [Fact]
    public void Resolve_DropsInvalidPaths()
    {
        var vdf = SteamTree.LibraryFoldersVdf(string.Empty, @"relative\lib", @"\\host\share", @"E:\Lib");

        var libraries = LibraryFolders.Resolve(@"C:\Steam", vdf, CandD);

        Assert.Equal([@"C:\Steam"], libraries);
    }

    [Fact]
    public void Resolve_DropsNetworkRoot()
    {
        Assert.Empty(LibraryFolders.Resolve(@"\\host\share", null, _ => true));
        Assert.Empty(LibraryFolders.Resolve(@"relative", null, _ => true));
        Assert.Empty(LibraryFolders.Resolve(@"E:\Steam", null, CandD));
        Assert.Equal([@"D:\Lib"], LibraryFolders.Resolve(@"\\host\share", SteamTree.LibraryFoldersVdf(@"D:\Lib"), _ => true));
    }

    [Fact]
    public void Resolve_DropsForwardSlashNetworkPath()
    {
        Assert.Equal([@"C:\Steam"], LibraryFolders.Resolve(@"C:\Steam", SteamTree.LibraryFoldersVdf("//host/share"), _ => true));
    }

    [Fact]
    public void Resolve_PassesNormalizedPathToDriveCheck()
    {
        var seen = new List<string>();

        _ = LibraryFolders.Resolve(@"C:\Steam", SteamTree.LibraryFoldersVdf(@"D:\Lib\"), path =>
        {
            seen.Add(path);
            return true;
        });

        Assert.Equal([@"C:\Steam", @"D:\Lib"], seen);
    }

    [Fact]
    public void Resolve_BadRoot_DoesNotThrow()
    {
        Assert.Empty(LibraryFolders.Resolve(string.Empty, null, CandD));
    }

    [Fact]
    public void Resolve_KeepsAtMost16()
    {
        var many = Enumerable.Range(0, 30).Select(i => @"D:\Lib" + i.ToString(CultureInfo.InvariantCulture)).ToArray();

        var libraries = LibraryFolders.Resolve(@"C:\Steam", SteamTree.LibraryFoldersVdf(many), CandD);

        Assert.Equal(16, libraries.Count);
        Assert.Equal(@"C:\Steam", libraries[0]);
        Assert.Equal(@"D:\Lib14", libraries[15]);
    }

    [Fact]
    public void Resolve_DropsLibraryThatIsAJunctionToOneAlreadyKept()
    {
        using var temp = new TempFolder();
        var real = Path.Combine(temp.Folder, "Real");
        var link = Path.Combine(temp.Folder, "Link");
        Directory.CreateDirectory(real);
        SteamTree.MakeJunction(link, real);

        try
        {
            var linkListedLast = LibraryFolders.Resolve(real, SteamTree.LibraryFoldersVdf(link), _ => true);
            var linkListedFirst = LibraryFolders.Resolve(link, SteamTree.LibraryFoldersVdf(real), _ => true);

            Assert.Equal([real], linkListedLast);
            Assert.Equal([link], linkListedFirst);
        }
        finally
        {
            Directory.Delete(link);
        }
    }
}
