using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SteamPathsTests
{
    private static bool Anywhere(string path) => true;

    [Fact]
    public void FinalPath_NoLink_ComesBackWithoutTrailingSeparator()
    {
        using var temp = new TempFolder();
        var plain = Path.Combine(temp.Folder, "plain");
        Directory.CreateDirectory(plain);

        Assert.Equal(plain, SteamPaths.FinalPath(plain + Path.DirectorySeparatorChar, Anywhere));
        Assert.Equal(Path.Combine(plain, "missing", "x.exe"), SteamPaths.FinalPath(Path.Combine(plain, "missing", "x.exe"), Anywhere));
        Assert.Equal(@"C:\", SteamPaths.FinalPath(@"C:\", Anywhere));
    }

    [Fact]
    public void FinalPath_ResolvesALinkOnAnyPart()
    {
        using var temp = new TempFolder();
        var target = Path.Combine(temp.Folder, "target");
        var link = Path.Combine(temp.Folder, "link");
        Directory.CreateDirectory(Path.Combine(target, "inner"));
        SteamTree.MakeJunction(link, target);

        try
        {
            Assert.Equal(target, SteamPaths.FinalPath(link, Anywhere));
            Assert.Equal(Path.Combine(target, "inner"), SteamPaths.FinalPath(Path.Combine(link, "inner"), Anywhere));
            Assert.Equal(Path.Combine(target, "missing"), SteamPaths.FinalPath(Path.Combine(link, "missing"), Anywhere));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void FinalPath_ResolvesLinksInsideTheTargetToo()
    {
        using var temp = new TempFolder();
        var real = Path.Combine(temp.Folder, "real");
        var inner = Path.Combine(temp.Folder, "viaInner");
        var outer = Path.Combine(temp.Folder, "outer");
        Directory.CreateDirectory(Path.Combine(real, "leaf"));
        SteamTree.MakeJunction(inner, real);
        Directory.CreateDirectory(outer);
        var outerLink = Path.Combine(outer, "hop");
        SteamTree.MakeJunction(outerLink, Path.Combine(inner, "leaf"));

        try
        {
            Assert.Equal(Path.Combine(real, "leaf"), SteamPaths.FinalPath(outerLink, Anywhere));
        }
        finally
        {
            Directory.Delete(outerLink);
            Directory.Delete(inner);
        }
    }

    [Fact]
    public void FinalPath_RefusedTarget_GivesNull_AndEveryTargetIsAsked()
    {
        using var temp = new TempFolder();
        var target = Path.Combine(temp.Folder, "target");
        var link = Path.Combine(temp.Folder, "link");
        Directory.CreateDirectory(target);
        SteamTree.MakeJunction(link, target);
        var asked = new List<string>();

        try
        {
            var accepted = SteamPaths.FinalPath(link, path =>
            {
                asked.Add(path);
                return true;
            });
            var refused = SteamPaths.FinalPath(link, _ => false);

            Assert.Equal(target, accepted);
            Assert.Equal([target], asked);
            Assert.Null(refused);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void NetworkTarget_RefusedWithoutOpening()
    {
        // A symbolic link needs a privilege, so the stored target is supplied directly.
        var opened = new List<string>();
        var asked = new List<string>();
        string? ReadLink(string path)
        {
            opened.Add(path);
            return path.EndsWith("link", StringComparison.Ordinal) ? @"\\host\share" : null;
        }

        var refused = SteamPaths.FinalPath(@"C:\games\link", target =>
        {
            asked.Add(target);
            return LocalPathPolicy.IsLocal(target);
        }, ReadLink);

        Assert.Null(refused);
        Assert.Equal([@"\\host\share"], asked);
        Assert.Equal([@"C:\games", @"C:\games\link"], opened);
        Assert.False(LocalPathPolicy.IsLocal(@"\\host\share"));
    }

    [Fact]
    public void FinalPath_LoopOrTooManyLinks_GivesNull()
    {
        using var temp = new TempFolder();
        var a = Path.Combine(temp.Folder, "a");
        var b = Path.Combine(temp.Folder, "b");
        Directory.CreateDirectory(a);
        SteamTree.MakeJunction(b, a);
        Directory.Delete(a);
        SteamTree.MakeJunction(a, b);

        try
        {
            Assert.Null(SteamPaths.FinalPath(a, Anywhere));
        }
        finally
        {
            Directory.Delete(a);
            Directory.Delete(b);
        }
    }

    [Fact]
    public void FinalPath_FilePath_ComesBackAsWritten_AndAFileLinkIsResolvedAndFiltered()
    {
        using var temp = new TempFolder();
        var real = Path.Combine(temp.Folder, "real.acf");
        var link = Path.Combine(temp.Folder, "link.acf");
        File.WriteAllText(real, "x");

        Assert.Equal(real, SteamPaths.FinalPath(real, Anywhere));
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // A file symbolic link needs the symbolic-link right (admin or Developer Mode); without it only the plain-file half is checked.
            return;
        }

        try
        {
            Assert.Equal(real, SteamPaths.FinalPath(link, Anywhere));
            Assert.Null(SteamPaths.FinalPath(link, _ => false));
        }
        finally
        {
            File.Delete(link);
        }
    }

    [Fact]
    public void FinalPath_ReadingALinkFails_GivesNull()
    {
        static string? Broken(string path) => throw new IOException("unreadable");

        Assert.Null(SteamPaths.FinalPath(@"C:\games\link", Anywhere, Broken));
    }
}
