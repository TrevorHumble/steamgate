using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class LocalPathPolicyTests
{
    [Fact]
    public void IsLocal_AcceptsLocalFolder_RejectsNetworkRelativeAndEmptyPaths()
    {
        using var temp = new TempFolder();

        Assert.True(LocalPathPolicy.IsLocal(temp.Folder));
        Assert.False(LocalPathPolicy.IsLocal(@"\\host\share\lib"));
        Assert.False(LocalPathPolicy.IsLocal(@"\\?\C:\Windows"));
        Assert.False(LocalPathPolicy.IsLocal(@"relative\lib"));
        Assert.False(LocalPathPolicy.IsLocal(string.Empty));
    }

    [Fact]
    public void MovedTargetAllowed_RealFolders_RefusesDriveRootSystemAndProgramFoldersExceptTheDefaultSteamLibrary()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var steamCommon = Path.Combine(programFilesX86, "Steam", "steamapps", "common");
        using var temp = new TempFolder();

        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.GetPathRoot(temp.Folder)!));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(windows));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.Combine(windows, "System32")));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.Combine(programFiles, "Other", "App")));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.Combine(programFilesX86, "Steam")));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.Combine(programFilesX86, "Steam", "steamapps")));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(programData));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(Path.Combine(programData, "SteamGate", "G")));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"\\host\share\lib"));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(steamCommon));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(Path.Combine(steamCommon, "G")));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(programFiles + "Evil"));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(Path.Combine(temp.Folder, "moved")));
    }

    [Fact]
    public void MovedTargetAllowed_GivenRoots_AppliesEachRule()
    {
        var roots = new PolicyRoots(@"C:\Sys", @"C:\Pf", @"C:\Pf86", @"C:\Pd", @"C:\Pf86\Steam\steamapps\common");

        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"D:\", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"C:\Sys", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"c:\sys\Sub\G", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"C:\Pf\G", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"c:\pd\SteamGate\G", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"C:\PdX\G", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"C:\Pf86\Steam\steamapps", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"C:\Pf86\Steam\steamapps\commonX\G", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"C:\Pf86\Steam\steamapps\common", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"C:\Pf86\steam\STEAMAPPS\common\G\Bin", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"C:\SysX\G", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"C:\PfX\G", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"D:\Games\G", roots));
    }

    [Fact]
    public void MovedTargetAllowed_SystemFolderWinsOverTheSteamConstant_AndOtherLibrariesAreUnaffected()
    {
        var roots = new PolicyRoots(@"D:\Win", @"D:\Pf", @"D:\Pf86", @"D:\Pd", @"D:\Win\Steam\steamapps\common");

        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"D:\Win\System32", roots));
        Assert.False(LocalPathPolicy.MovedTargetAllowed(@"D:\Win\Steam\steamapps\common\G", roots));
        Assert.True(LocalPathPolicy.MovedTargetAllowed(@"D:\Lib\steamapps\common\G", roots));
    }

    [Fact]
    public void IsUnder_HandlesDriveRootParentsSiblingsAndCase()
    {
        Assert.True(LocalPathPolicy.IsUnder(@"D:\x\y", @"D:\"));
        Assert.True(LocalPathPolicy.IsUnder(@"D:\", @"D:\"));
        Assert.False(LocalPathPolicy.IsUnder(@"E:\x", @"D:\"));
        Assert.True(LocalPathPolicy.IsUnder(@"C:\Windows", @"c:\windows\"));
        Assert.True(LocalPathPolicy.IsUnder(@"C:\Windows\System32", @"C:\Windows"));
        Assert.False(LocalPathPolicy.IsUnder(@"C:\WindowsApps\x", @"C:\Windows"));
        Assert.False(LocalPathPolicy.IsUnder(@"C:\Windows", @"C:\Windows\System32"));
        Assert.False(LocalPathPolicy.IsUnder(@"relative", @"C:\Windows"));
        Assert.False(LocalPathPolicy.IsUnder(@"C:\Windows", string.Empty));
    }

    [Fact]
    public void IsLocal_JunctionToLocalFolder_IsAccepted()
    {
        using var temp = new TempFolder();
        var target = Path.Combine(temp.Folder, "target");
        var link = Path.Combine(temp.Folder, "link");
        Directory.CreateDirectory(target);
        SteamTree.MakeJunction(link, target);

        try
        {
            Assert.True(LocalPathPolicy.IsLocal(link));
        }
        finally
        {
            // Removes only the junction, so the temp folder can be deleted.
            Directory.Delete(link);
        }
    }

    [Fact]
    public void IsLocal_JunctionInTheMiddleOfThePath_IsFollowed()
    {
        using var temp = new TempFolder();
        var target = Path.Combine(temp.Folder, "target");
        var link = Path.Combine(temp.Folder, "link");
        Directory.CreateDirectory(Path.Combine(target, "inner"));
        SteamTree.MakeJunction(link, target);

        try
        {
            Assert.True(LocalPathPolicy.IsLocal(Path.Combine(link, "inner")));
            Assert.True(LocalPathPolicy.IsLocal(Path.Combine(link, "missing")));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void IsLocal_JunctionLoop_IsRefused()
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
            Assert.False(LocalPathPolicy.IsLocal(a));
        }
        finally
        {
            Directory.Delete(a);
            Directory.Delete(b);
        }
    }

    [Fact]
    public void Usable_NormalizesAndAsksTheCheckOnce()
    {
        var asked = new List<string>();

        var usable = LocalPathPolicy.Usable(@"D:\Lib\", path =>
        {
            asked.Add(path);
            return true;
        });

        Assert.Equal(@"D:\Lib", usable);
        Assert.Equal([@"D:\Lib"], asked);
    }

    [Fact]
    public void Usable_DropsRelativeNetworkAndRefusedPaths_WithoutAskingTheCheck()
    {
        var asked = 0;

        bool Count(string path)
        {
            asked++;
            return true;
        }

        Assert.Null(LocalPathPolicy.Usable(@"relative\lib", Count));
        Assert.Null(LocalPathPolicy.Usable(@"\\host\share", Count));
        Assert.Null(LocalPathPolicy.Usable("//host/share", Count));
        Assert.Null(LocalPathPolicy.Usable("C:\\bad\0path", Count));
        Assert.Equal(0, asked);
        Assert.Null(LocalPathPolicy.Usable(@"E:\Lib", _ => false));
        Assert.Equal(@"D:\", LocalPathPolicy.Usable(@"D:\", _ => true));
    }
}
