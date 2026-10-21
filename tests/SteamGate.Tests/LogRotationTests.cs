using SteamGate.Core;
using Xunit;

namespace SteamGate.Tests;

public sealed class LogRotationTests
{
    private const int OneMegabyte = 1048576;

    [Fact]
    public void RotatesPast1MB()
    {
        Assert.True(LogRotation.ShouldRotate(OneMegabyte + 1));
        Assert.False(LogRotation.ShouldRotate(OneMegabyte));
    }

    [Fact]
    public void ShouldRotate_SmallOrEmpty_ReturnsFalse()
    {
        Assert.False(LogRotation.ShouldRotate(0));
        Assert.False(LogRotation.ShouldRotate(1));
    }

    [Fact]
    public void RollIfTooLarge_PastLimit_MovesFileAndReplacesOldRoll()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("service.log");
        File.WriteAllBytes(path, new byte[OneMegabyte + 1]);
        File.WriteAllText(path + ".1", "older");

        LogRotation.RollIfTooLarge(path);

        Assert.False(File.Exists(path));
        Assert.Equal(OneMegabyte + 1, new FileInfo(path + ".1").Length);
    }

    [Fact]
    public void RollIfTooLarge_AtLimit_LeavesFile()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("service.log");
        File.WriteAllBytes(path, new byte[OneMegabyte]);

        LogRotation.RollIfTooLarge(path);

        Assert.Equal(OneMegabyte, new FileInfo(path).Length);
        Assert.False(File.Exists(path + ".1"));
    }

    [Fact]
    public void RollIfTooLarge_MissingFile_DoesNothing()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("service.log");

        LogRotation.RollIfTooLarge(path);

        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".1"));
    }
}
