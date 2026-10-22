using System.Text;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SteamFilesTests
{
    [Fact]
    public void ReadCapped_LockedMissingOversized()
    {
        using var temp = new TempFolder();
        var lockedPath = temp.PathOf("locked.vdf");
        File.WriteAllText(lockedPath, "x");
        var bigPath = temp.PathOf("big.vdf");
        File.WriteAllText(bigPath, new string('a', SteamFiles.MaxVdfBytes + 1));

        (string? Text, bool Locked) locked;
        using (new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            locked = SteamFiles.ReadCapped(lockedPath);
        }

        Assert.Equal((null, true), locked);
        Assert.Equal((null, false), SteamFiles.ReadCapped(temp.PathOf("missing.vdf")));
        Assert.Equal((null, false), SteamFiles.ReadCapped(bigPath));
    }

    [Fact]
    public void ReadCapped_GivenALimit_ReadsAtMostThatManyBytes()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("f.vdf"), new string('a', 100));

        Assert.Equal((new string('a', 100), false), SteamFiles.ReadCapped(temp.PathOf("f.vdf"), 100, out var atLimit));
        Assert.Equal(100, atLimit);
        Assert.Equal((null, false), SteamFiles.ReadCapped(temp.PathOf("f.vdf"), 99, out var over));
        Assert.Equal(99, over);
        Assert.Equal((null, false), SteamFiles.ReadCapped(temp.PathOf("f.vdf"), 0, out var none));
        Assert.Equal(0, none);
        Assert.Equal((null, false), SteamFiles.ReadCapped(temp.PathOf("missing.vdf"), 50, out var missing));
        Assert.Equal(0, missing);
    }

    [Fact]
    public void ReadCapped_FileAtTheCap_IsRead()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("cap.vdf"), new string('a', SteamFiles.MaxVdfBytes));

        var (text, locked) = SteamFiles.ReadCapped(temp.PathOf("cap.vdf"));

        Assert.Equal(SteamFiles.MaxVdfBytes, text?.Length);
        Assert.False(locked);
    }

    [Fact]
    public void ReadCapped_ReturnsTheText_WithoutBom()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("a.vdf"), "\"k\" \"v\"", new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(temp.PathOf("b.vdf"), "\"k\" \"v\"", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        File.WriteAllBytes(temp.PathOf("c.vdf"), []);

        Assert.Equal(("\"k\" \"v\"", false), SteamFiles.ReadCapped(temp.PathOf("a.vdf")));
        Assert.Equal(("\"k\" \"v\"", false), SteamFiles.ReadCapped(temp.PathOf("b.vdf")));
        Assert.Equal((string.Empty, false), SteamFiles.ReadCapped(temp.PathOf("c.vdf")));
    }

    [Fact]
    public void ReadCapped_MissingFolder_IsNotLocked()
    {
        using var temp = new TempFolder();

        Assert.Equal((null, false), SteamFiles.ReadCapped(Path.Combine(temp.Folder, "nope", "x.vdf")));
    }

    [Fact]
    public void ReadCapped_InvalidPath_DoesNotThrow()
    {
        Assert.Equal((null, false), SteamFiles.ReadCapped("bad\0path"));
    }

    [Fact]
    public void ReadCapped_PathIsAFolder_IsLocked()
    {
        using var temp = new TempFolder();

        Assert.Equal((null, true), SteamFiles.ReadCapped(temp.Folder));
    }
}
