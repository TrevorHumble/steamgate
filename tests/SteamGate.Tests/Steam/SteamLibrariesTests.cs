using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SteamLibrariesTests
{
    private static bool Anywhere(string path) => true;

    [Fact]
    public void RefreshesEvery60Seconds()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        SteamTree.MakeGameFolder(root, "Game");
        SteamTree.WriteVdf(root, SteamTree.LibraryFoldersVdf(root));
        var steam = new SteamLibraries();

        var first = steam.Refresh(() => root, TimeSpan.Zero, Anywhere, _ => { });
        SteamTree.MakeGameFolder(root, "Later");
        var early = steam.Refresh(() => root, TimeSpan.FromSeconds(59), Anywhere, _ => { });
        var due = steam.Refresh(() => root, TimeSpan.FromSeconds(60), Anywhere, _ => { });
        var afterDue = steam.Refresh(() => root, TimeSpan.FromSeconds(119), Anywhere, _ => { });
        var dueAgain = steam.Refresh(() => root, TimeSpan.FromSeconds(120), Anywhere, _ => { });

        Assert.Equal([true, false, true, false, true], new[] { first.Refreshed, early.Refreshed, due.Refreshed, afterDue.Refreshed, dueAgain.Refreshed });
        Assert.Equal([root], first.Libraries);
        Assert.Equal(["Game|Game"], SteamTree.Pairs(first.Installed));
        Assert.Equal(["Game|Game"], SteamTree.Pairs(early.Installed));
        Assert.Equal(["Game|Game", "Later|Later"], SteamTree.Pairs(due.Installed));
        Assert.Equal(["Game|Game", "Later|Later"], SteamTree.Pairs(afterDue.Installed));
    }

    [Fact]
    public void LockedVdfKeepsPrevious()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var other = Path.Combine(temp.Folder, "Other");
        SteamTree.MakeGameFolder(other, "Far");
        SteamTree.WriteVdf(root, SteamTree.LibraryFoldersVdf(root, other));
        var steam = new SteamLibraries();
        var first = steam.Refresh(() => root, TimeSpan.Zero, Anywhere, _ => { });

        (IReadOnlyList<string> Libraries, IReadOnlyList<InstalledApp> Installed, IReadOnlyList<string> CommonTargets, bool Refreshed) locked;
        using (new FileStream(SteamLayout.LibraryFoldersFile(root), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            locked = steam.Refresh(() => root, TimeSpan.FromSeconds(60), Anywhere, _ => { });
        }

        Assert.Equal([root, other], first.Libraries);
        Assert.True(locked.Refreshed);
        Assert.Equal([root, other], locked.Libraries);
        Assert.Equal(["Far|Far"], SteamTree.Pairs(locked.Installed));
    }

    [Fact]
    public void LockedVdfOnFirstRead_GivesRootAlone()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        SteamTree.WriteVdf(root, SteamTree.LibraryFoldersVdf(root, Path.Combine(temp.Folder, "Other")));

        using var held = new FileStream(SteamLayout.LibraryFoldersFile(root), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, Anywhere, _ => { });

        Assert.Equal([root], result.Libraries);
    }

    [Fact]
    public void MissingVdf_GivesRootAlone_AndSkippedManifestsAreReported()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "not vdf");
        SteamTree.MakeGameFolder(temp.Folder, "G");
        var skipped = new List<string>();

        var result = new SteamLibraries().Refresh(() => temp.Folder, TimeSpan.Zero, Anywhere, skipped.Add);

        Assert.Equal([temp.Folder], result.Libraries);
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_1.acf")], skipped);
    }

    [Fact]
    public void LockedVdfWithChangedRoot_GivesNewRootAlone()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var other = Path.Combine(temp.Folder, "Other");
        var newRoot = Path.Combine(temp.Folder, "NewSteam");
        SteamTree.WriteVdf(root, SteamTree.LibraryFoldersVdf(root, other));
        SteamTree.WriteVdf(newRoot, SteamTree.LibraryFoldersVdf(newRoot, other));
        var steam = new SteamLibraries();
        _ = steam.Refresh(() => root, TimeSpan.Zero, Anywhere, _ => { });

        using var held = new FileStream(SteamLayout.LibraryFoldersFile(newRoot), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = steam.Refresh(() => newRoot, TimeSpan.FromSeconds(60), Anywhere, _ => { });

        Assert.Equal([newRoot], result.Libraries);
    }

    [Fact]
    public void LocateRoot_IsCalledOnlyWhenAReadIsDue()
    {
        using var temp = new TempFolder();
        var calls = 0;
        string Locate()
        {
            calls++;
            return temp.Folder;
        }

        var steam = new SteamLibraries();
        _ = steam.Refresh(Locate, TimeSpan.Zero, Anywhere, _ => { });
        _ = steam.Refresh(Locate, TimeSpan.FromSeconds(59), Anywhere, _ => { });
        _ = steam.Refresh(Locate, TimeSpan.FromSeconds(60), Anywhere, _ => { });

        Assert.Equal(2, calls);
    }


    // Refreshes once and matches processes the way the worker does.
    private static IReadOnlyList<RunningGame> RefreshAndMatch(string root, Func<string, bool> isLocalDrive, params string[] exePaths)
    {
        var state = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, isLocalDrive, _ => { });
        var processes = exePaths.Select((path, i) => new ProcessSample(i + 1, DateTime.UnixEpoch, path));
        return GameMatcher.Match(processes, state.Libraries, state.Installed, [], state.CommonTargets);
    }

    [Fact]
    public void JunctionedGameIsSeen()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var moved = Path.Combine(temp.Folder, "moved", "Game");
        Directory.CreateDirectory(moved);
        Directory.CreateDirectory(SteamLayout.CommonFolder(root));
        var link = Path.Combine(SteamLayout.CommonFolder(root), "Game");
        SteamTree.MakeJunction(link, moved);

        try
        {
            var games = RefreshAndMatch(root, Anywhere, Path.Combine(moved, "g.exe"), Path.Combine(temp.Folder, "moved", "Other", "o.exe"));

            var game = Assert.Single(games);
            Assert.Equal("Game", game.Folder);
            Assert.Equal([1], game.Processes.Select(process => process.Pid));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void JunctionedLibraryIsSeen()
    {
        using var temp = new TempFolder();
        var real = Path.Combine(temp.Folder, "real");
        var root = Path.Combine(temp.Folder, "Steam");
        SteamTree.MakeGameFolder(real, "Game");
        SteamTree.WriteManifest(real, "appmanifest_1.acf", SteamTree.Manifest("The Game", "Game"));
        SteamTree.MakeJunction(root, real);

        try
        {
            var state = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, Anywhere, _ => { });
            var games = GameMatcher.Match(
                [new ProcessSample(1, DateTime.UnixEpoch, Path.Combine(real, "steamapps", "common", "Game", "g.exe"))],
                state.Libraries,
                state.Installed,
                [],
                state.CommonTargets);

            var game = Assert.Single(games);
            Assert.Equal(("Game", "The Game"), (game.Folder, game.DisplayName));
            Assert.Equal([root], state.Libraries);
            Assert.Equal([SteamLayout.CommonFolder(real)], state.CommonTargets);
        }
        finally
        {
            Directory.Delete(root);
        }
    }

    [Fact]
    public void JunctionedCommonIsSeen()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var movedCommon = Path.Combine(temp.Folder, "moved", "common");
        Directory.CreateDirectory(Path.Combine(movedCommon, "Game"));
        Directory.CreateDirectory(SteamLayout.SteamAppsFolder(root));
        var link = SteamLayout.CommonFolder(root);
        SteamTree.MakeJunction(link, movedCommon);

        try
        {
            var games = RefreshAndMatch(root, Anywhere, Path.Combine(movedCommon, "Game", "g.exe"));

            var game = Assert.Single(games);
            Assert.Equal("Game", game.Folder);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void JunctionedSteamappsIsSeen()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var movedSteamapps = Path.Combine(temp.Folder, "moved", "steamapps");
        Directory.CreateDirectory(Path.Combine(movedSteamapps, "common", "Game"));
        Directory.CreateDirectory(root);
        var link = SteamLayout.SteamAppsFolder(root);
        SteamTree.MakeJunction(link, movedSteamapps);

        try
        {
            var games = RefreshAndMatch(root, Anywhere, Path.Combine(movedSteamapps, "common", "Game", "g.exe"));

            Assert.Equal("Game", Assert.Single(games).Folder);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void JunctionedGameInsideAJunctionedCommon_IsSeenAtItsFinalPath()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var movedCommon = Path.Combine(temp.Folder, "moved", "common");
        var elsewhere = Path.Combine(temp.Folder, "elsewhere", "Game");
        Directory.CreateDirectory(movedCommon);
        Directory.CreateDirectory(elsewhere);
        Directory.CreateDirectory(SteamLayout.SteamAppsFolder(root));
        var commonLink = SteamLayout.CommonFolder(root);
        SteamTree.MakeJunction(commonLink, movedCommon);
        var gameLink = Path.Combine(movedCommon, "Game");
        SteamTree.MakeJunction(gameLink, elsewhere);

        try
        {
            var games = RefreshAndMatch(root, Anywhere, Path.Combine(elsewhere, "g.exe"));

            Assert.Equal("Game", Assert.Single(games).Folder);
        }
        finally
        {
            Directory.Delete(gameLink);
            Directory.Delete(commonLink);
        }
    }

    [Fact]
    public void JunctionToNonLocalTarget_IsIgnored()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var moved = Path.Combine(temp.Folder, "moved", "Game");
        Directory.CreateDirectory(moved);
        Directory.CreateDirectory(SteamLayout.CommonFolder(root));
        var link = Path.Combine(SteamLayout.CommonFolder(root), "Game");
        SteamTree.MakeJunction(link, moved);

        try
        {
            var state = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, path => !path.Contains("moved", StringComparison.Ordinal), _ => { });

            var game = Assert.Single(state.Installed);
            Assert.Null(game.MovedTo);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void LibraryWhoseCommonFolderPointsOffTheLocalDrives_IsLeftOut()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        var other = Path.Combine(temp.Folder, "Other");
        var offDrive = Path.Combine(temp.Folder, "offdrive", "common");
        Directory.CreateDirectory(Path.Combine(offDrive, "Hidden"));
        Directory.CreateDirectory(SteamLayout.SteamAppsFolder(other));
        SteamTree.MakeGameFolder(root, "Game");
        SteamTree.WriteVdf(root, SteamTree.LibraryFoldersVdf(root, other));
        var link = SteamLayout.CommonFolder(other);
        SteamTree.MakeJunction(link, offDrive);

        try
        {
            var state = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, path => !path.Contains("offdrive", StringComparison.Ordinal), _ => { });

            Assert.Equal([root], state.Libraries);
            Assert.Equal(["Game|Game"], SteamTree.Pairs(state.Installed));
            Assert.Empty(state.CommonTargets);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void LibraryWhoseSteamappsOrCommonFails_IsNotRead()
    {
        using var temp = new TempFolder();
        var root = Path.Combine(temp.Folder, "Steam");
        SteamTree.MakeGameFolder(root, "Game");
        var asked = new List<string>();

        bool Record(string path)
        {
            asked.Add(path);
            return !path.EndsWith("common", StringComparison.Ordinal);
        }

        var state = new SteamLibraries().Refresh(() => root, TimeSpan.Zero, Record, _ => { });

        Assert.Empty(state.Libraries);
        Assert.Empty(state.Installed);
        Assert.Equal([root, SteamLayout.SteamAppsFolder(root), SteamLayout.CommonFolder(root)], asked);
    }

    [Fact]
    public void BadManifest_IsLoggedOnceWhileItStaysBad_AndAgainWhenItReturns()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "not vdf");
        SteamTree.MakeGameFolder(temp.Folder, "G");
        var logged = new List<string>();
        var steam = new SteamLibraries();
        var bad = Path.Combine(temp.Folder, "steamapps", "appmanifest_1.acf");

        _ = steam.Refresh(() => temp.Folder, TimeSpan.Zero, Anywhere, logged.Add);
        _ = steam.Refresh(() => temp.Folder, TimeSpan.FromSeconds(60), Anywhere, logged.Add);
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", SteamTree.Manifest("Fixed", "G"));
        _ = steam.Refresh(() => temp.Folder, TimeSpan.FromSeconds(120), Anywhere, logged.Add);
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "not vdf");
        _ = steam.Refresh(() => temp.Folder, TimeSpan.FromSeconds(180), Anywhere, logged.Add);

        Assert.Equal([bad, bad], logged);
    }
}
