using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class GameMatcherTests
{
    private static readonly string[] NoNames = [];

    private static DateTime StartOf(int pid) => new(2026, 1, 1, 0, 0, pid, DateTimeKind.Utc);

    private static ProcessSample Proc(int pid, string path) => new(pid, StartOf(pid), path);

    [Fact]
    public void Match_ExcludesNotCountedAndNonGames()
    {
        var processes = new[]
        {
            Proc(10, @"C:\Steam\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe"),
            Proc(11, @"D:\Lib\steamapps\common\Blender\blender.exe"),
            Proc(12, @"C:\Windows\explorer.exe"),
            Proc(13, @"c:\steam\STEAMAPPS\common\cyberpunk 2077\launcher.exe"),
        };
        InstalledApp[] installed = [new("Cyberpunk 2077", "Cyberpunk 2077"), new("Blender", "Blender")];

        var games = GameMatcher.Match(processes, [@"C:\Steam", @"D:\Lib"], installed, ["Blender"]);

        var game = Assert.Single(games);
        Assert.Equal("Cyberpunk 2077", game.Folder);
        Assert.Equal("Cyberpunk 2077", game.DisplayName);
        Assert.Equal([(10, StartOf(10)), (13, StartOf(13))], game.Processes.Select(process => (process.Pid, process.StartUtc)));
    }

    [Fact]
    public void Match_FolderBoundaryAndDriveRoot()
    {
        var processes = new[]
        {
            Proc(5, @"C:\Steam\steamapps\common\Cyberpunk 2077 Mods\x.exe"),
            Proc(6, @"D:\steamapps\common\Viewfinder\v.exe"),
        };

        var games = GameMatcher.Match(processes, [@"C:\Steam", @"D:\"], [], NoNames);

        Assert.Equal(["Cyberpunk 2077 Mods", "Viewfinder"], games.Select(game => game.Folder));
    }

    [Fact]
    public void Match_FolderBoundary_ShorterInstalledNameDoesNotClaimLongerFolder()
    {
        var processes = new[] { Proc(5, @"C:\Steam\steamapps\common\Cyberpunk 2077 Mods\x.exe") };
        InstalledApp[] installed = [new("Cyberpunk 2077", "Cyberpunk 2077")];

        var games = GameMatcher.Match(processes, [@"C:\Steam"], installed, NoNames);

        var game = Assert.Single(games);
        Assert.Equal("Cyberpunk 2077 Mods", game.Folder);
        Assert.Equal("Cyberpunk 2077 Mods", game.DisplayName);
    }

    [Fact]
    public void Match_UninstalledSpellingFromLowestPid()
    {
        var processes = new[]
        {
            Proc(31, @"C:\Steam\steamapps\common\game x\a.exe"),
            Proc(30, @"C:\Steam\steamapps\common\Game X\b.exe"),
        };

        var games = GameMatcher.Match(processes, [@"C:\Steam"], [], NoNames);

        var game = Assert.Single(games);
        Assert.Equal("Game X", game.Folder);
        Assert.Equal("Game X", game.DisplayName);
        Assert.Equal([30, 31], game.Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_SteamClientIsNotAGame()
    {
        var steamExe = new[] { Proc(1, @"C:\Steam\steam.exe") };
        var looseFile = new[] { Proc(2, @"C:\Steam\steamapps\common\foo.exe") };

        Assert.Empty(GameMatcher.Match(steamExe, [@"C:\Steam"], [], NoNames));
        Assert.Empty(GameMatcher.Match(looseFile, [@"C:\Steam"], [], NoNames));
    }

    [Fact]
    public void Match_NotCountedIgnoresCase()
    {
        var processes = new[] { Proc(1, @"C:\Steam\steamapps\common\Blender\blender.exe") };

        Assert.Empty(GameMatcher.Match(processes, [@"C:\Steam"], [], ["blender"]));
    }

    [Fact]
    public void Match_DisplayNameComesFromInstalledEntry()
    {
        var processes = new[] { Proc(1, @"C:\Steam\steamapps\common\wallpaper_engine\w.exe") };
        InstalledApp[] installed = [new("Wallpaper_Engine", "Wallpaper Engine")];

        var game = Assert.Single(GameMatcher.Match(processes, [@"C:\Steam"], installed, NoNames));

        Assert.Equal("Wallpaper_Engine", game.Folder);
        Assert.Equal("Wallpaper Engine", game.DisplayName);
    }

    [Fact]
    public void Match_DuplicateInstalledFolders_UsesFirst()
    {
        var processes = new[] { Proc(1, @"C:\Steam\steamapps\common\Shared\s.exe") };
        InstalledApp[] installed = [new("Shared", "First"), new("shared", "Second")];

        var game = Assert.Single(GameMatcher.Match(processes, [@"C:\Steam"], installed, NoNames));

        Assert.Equal("First", game.DisplayName);
    }

    [Fact]
    public void Match_GamesSortedByFolder_ProcessesByPid()
    {
        var processes = new[]
        {
            Proc(9, @"C:\Steam\steamapps\common\beta\a.exe"),
            Proc(8, @"C:\Steam\steamapps\common\Alpha\a.exe"),
            Proc(3, @"C:\Steam\steamapps\common\beta\b.exe"),
        };

        var games = GameMatcher.Match(processes, [@"C:\Steam"], [], NoNames);

        Assert.Equal(["Alpha", "beta"], games.Select(game => game.Folder));
        Assert.Equal([3, 9], games[1].Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_UnreadablePath_IsSkipped()
    {
        var processes = new[] { Proc(1, "bad\0path"), Proc(2, @"C:\Steam\steamapps\common\G\a.exe") };

        var game = Assert.Single(GameMatcher.Match(processes, [@"C:\Steam"], [], NoNames));

        Assert.Equal([2], game.Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_NoProcessesOrNoLibraries_ReturnsEmpty()
    {
        Assert.Empty(GameMatcher.Match([], [@"C:\Steam"], [], NoNames));
        Assert.Empty(GameMatcher.Match([Proc(1, @"C:\Steam\steamapps\common\G\a.exe")], [], [], NoNames));
    }

    [Fact]
    public void Match_ProcessUnderAMovedFolder_IsTheSteamFolderName()
    {
        var processes = new[]
        {
            Proc(1, @"E:\moved\Game\bin\g.exe"),
            Proc(2, @"E:\moved\Game2\g.exe"),
            Proc(3, @"E:\moved\GAME\h.exe"),
        };
        InstalledApp[] installed = [new("Game", "The Game", @"E:\moved\Game"), new("Plain", "Plain")];

        var games = GameMatcher.Match(processes, [@"C:\Steam"], installed, NoNames);

        var game = Assert.Single(games);
        Assert.Equal(("Game", "The Game"), (game.Folder, game.DisplayName));
        Assert.Equal([1, 3], game.Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_MovedFolderOnTheNotCountedList_IsLeftOut()
    {
        InstalledApp[] installed = [new("Blender", "Blender", @"E:\moved\Blender")];

        Assert.Empty(GameMatcher.Match([Proc(1, @"E:\moved\Blender\b.exe")], [@"C:\Steam"], installed, ["blender"]));
    }

    [Fact]
    public void Match_ProcessesCarryTheirPathAndStartTime()
    {
        var sample = Proc(4, @"C:\Steam\steamapps\common\G\a.exe");

        var game = Assert.Single(GameMatcher.Match([sample], [@"C:\Steam"], [], NoNames));

        Assert.Equal([sample], game.Processes);
    }

    [Fact]
    public void Match_ProcessUnderAMovedCommonFolder_IsTheSteamFolderName()
    {
        var processes = new[]
        {
            Proc(1, @"E:\real\steamapps\common\Game\bin\g.exe"),
            Proc(2, @"E:\real\steamapps\common2\Other\o.exe"),
            Proc(3, @"E:\real\steamapps\common\loose.exe"),
        };

        var games = GameMatcher.Match(processes, [@"C:\Steam"], [], NoNames, [@"E:\real\steamapps\common"]);

        var game = Assert.Single(games);
        Assert.Equal("Game", game.Folder);
        Assert.Equal([1], game.Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_MovedCommonFolderOnTheNotCountedList_IsLeftOut()
    {
        var processes = new[] { Proc(1, @"E:\real\steamapps\common\Blender\x.exe") };

        Assert.Empty(GameMatcher.Match(processes, [@"C:\Steam"], [], ["blender"], [@"E:\real\steamapps\common"]));
    }

    [Fact]
    public void Match_FinalPathThatIsADriveRoot_StillMatches()
    {
        InstalledApp[] installed = [new("Moved", "Moved", @"F:\")];

        var common = GameMatcher.Match([Proc(1, @"E:\Game\g.exe")], [@"C:\Steam"], [], NoNames, [@"E:\"]);
        var moved = GameMatcher.Match([Proc(2, @"F:\h.exe"), Proc(3, @"F:\sub\i.exe")], [@"C:\Steam"], installed, NoNames);

        var onCommon = Assert.Single(common);
        Assert.Equal("Game", onCommon.Folder);
        Assert.Equal([1], onCommon.Processes.Select(process => process.Pid));
        var onMoved = Assert.Single(moved);
        Assert.Equal("Moved", onMoved.Folder);
        Assert.Equal([2, 3], onMoved.Processes.Select(process => process.Pid));
    }

    [Fact]
    public void Match_NeverMatchesUnderSystemRoot()
    {
        // Each way a path can be matched (library, moved common folder, moved game) is aimed at the system folder.
        var processes = new[]
        {
            Proc(1, @"C:\Windows\System32\Sub\a.exe"),
            Proc(2, @"c:\WINDOWS\Moved\b.exe"),
            Proc(3, @"C:\Windows\steamapps\common\Lib\c.exe"),
            Proc(4, @"C:\WindowsApps\Fine\d.exe"),
        };
        InstalledApp[] installed = [new("Moved", "Moved", @"C:\Windows\Moved")];
        string[] commonTargets = [@"C:\Windows\System32", @"C:\WindowsApps"];

        var guarded = GameMatcher.Match(processes, [@"C:\Windows"], installed, NoNames, commonTargets, @"C:\Windows");
        var unguarded = GameMatcher.Match(processes, [@"C:\Windows"], installed, NoNames, commonTargets, @"Z:\Elsewhere");
        var real = GameMatcher.Match(processes, [@"C:\Windows"], installed, NoNames, commonTargets);

        Assert.Equal(["Fine"], guarded.Select(game => game.Folder));
        Assert.Equal(["Fine", "Lib", "Moved", "Sub"], unguarded.Select(game => game.Folder));
        Assert.Equal(["Fine"], real.Select(game => game.Folder));
    }

    [Fact]
    public void Match_SystemRootAsADriveRoot_BlocksTheWholeDrive()
    {
        var processes = new[] { Proc(1, @"D:\steamapps\common\G\g.exe"), Proc(2, @"E:\steamapps\common\H\h.exe") };

        var games = GameMatcher.Match(processes, [@"D:\", @"E:\"], [], NoNames, null, @"D:\");

        Assert.Equal(["H"], games.Select(game => game.Folder));
    }
}
