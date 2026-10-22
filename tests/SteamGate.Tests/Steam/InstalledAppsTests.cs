using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class InstalledAppsTests
{
    private static bool Anywhere(string path) => true;

    [Fact]
    public void Read_ManifestAndBareFolder()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1382070.acf", SteamTree.Manifest("Viewfinder", "Viewfinder"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1174180.acf", SteamTree.Manifest("Red Dead Redemption 2", "Red Dead Redemption 2"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1404210.acf", SteamTree.Manifest("Red Dead Online", "Red Dead Redemption 2"));
        SteamTree.MakeGameFolder(temp.Folder, "Viewfinder");
        SteamTree.MakeGameFolder(temp.Folder, "Red Dead Redemption 2");
        SteamTree.MakeGameFolder(temp.Folder, "Arc Raiders");

        var apps = InstalledApps.Read(temp.Folder, _ => { }, Anywhere, new ManifestBudget());

        Assert.Equal(
            ["Arc Raiders|Arc Raiders", "Red Dead Redemption 2|Red Dead Redemption 2", "Viewfinder|Viewfinder"],
            SteamTree.Pairs(apps));
    }

    [Fact]
    public void Read_LowestAppIdWins_SkipsAbsentAndMalformed()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_99.acf", SteamTree.Manifest("Zed", "Shared"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1000.acf", SteamTree.Manifest("Alpha", "Shared"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_5.acf", SteamTree.Manifest("Gone", "Gone"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_6.acf", "not vdf");
        SteamTree.MakeGameFolder(temp.Folder, "Shared");
        var skipped = new List<string>();

        var apps = InstalledApps.Read(temp.Folder, skipped.Add, Anywhere, new ManifestBudget());

        Assert.Equal(["Shared|Zed"], SteamTree.Pairs(apps));
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_6.acf")], skipped);
    }

    [Fact]
    public void Read_OversizedManifest_Skipped()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_7.acf", new string(' ', SteamFiles.MaxVdfBytes + 1));
        SteamTree.MakeGameFolder(temp.Folder, "Big");
        var skipped = new List<string>();

        var apps = InstalledApps.Read(temp.Folder, skipped.Add, Anywhere, new ManifestBudget());

        Assert.Equal(["Big|Big"], SteamTree.Pairs(apps));
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_7.acf")], skipped);
    }

    [Fact]
    public void Read_MissingRoot_ReturnsEmpty()
    {
        using var temp = new TempFolder();

        Assert.Empty(InstalledApps.Read(Path.Combine(temp.Folder, "nope"), _ => { }, Anywhere, new ManifestBudget()));
        Assert.Empty(InstalledApps.Read(temp.Folder, _ => { }, Anywhere, new ManifestBudget()));
    }

    [Fact]
    public void Read_ManifestAtMaxSize_IsRead()
    {
        using var temp = new TempFolder();
        var manifest = SteamTree.Manifest("Named", "G");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", manifest + new string(' ', SteamFiles.MaxVdfBytes - manifest.Length));
        SteamTree.MakeGameFolder(temp.Folder, "G");

        Assert.Equal(["G|Named"], SteamTree.Pairs(SteamTree.ReadAll(temp.Folder)));
    }

    [Fact]
    public void Read_NameMissingOrEmpty_ShowsFolderName()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", SteamTree.Manifest(null, "NoName"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf", SteamTree.Manifest(string.Empty, "EmptyName"));
        SteamTree.MakeGameFolder(temp.Folder, "NoName");
        SteamTree.MakeGameFolder(temp.Folder, "EmptyName");

        Assert.Equal(["EmptyName|EmptyName", "NoName|NoName"], SteamTree.Pairs(SteamTree.ReadAll(temp.Folder)));
    }

    [Fact]
    public void Read_InstallDirIgnoresCase_FolderSpellingFromDisk()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", SteamTree.Manifest("Pretty Name", "game one"));
        SteamTree.MakeGameFolder(temp.Folder, "Game One");

        Assert.Equal(["Game One|Pretty Name"], SteamTree.Pairs(SteamTree.ReadAll(temp.Folder)));
    }

    [Fact]
    public void Read_SortsByDisplayNameIgnoringCase()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", SteamTree.Manifest("banana", "A"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf", SteamTree.Manifest("Apple", "B"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_3.acf", SteamTree.Manifest("cherry", "C"));
        SteamTree.MakeGameFolder(temp.Folder, "A");
        SteamTree.MakeGameFolder(temp.Folder, "B");
        SteamTree.MakeGameFolder(temp.Folder, "C");

        Assert.Equal(["B|Apple", "A|banana", "C|cherry"], SteamTree.Pairs(SteamTree.ReadAll(temp.Folder)));
    }

    [Fact]
    public void Read_ManifestNameThatIsNotANumber_Skipped()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_abc.acf", SteamTree.Manifest("X", "G"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_.acf", SteamTree.Manifest("X", "G"));
        SteamTree.WriteManifest(temp.Folder, "appmanifest_-4.acf", SteamTree.Manifest("X", "G"));
        SteamTree.MakeGameFolder(temp.Folder, "G");
        var skipped = new List<string>();

        var apps = InstalledApps.Read(temp.Folder, skipped.Add, Anywhere, new ManifestBudget());

        Assert.Equal(["G|G"], SteamTree.Pairs(apps));
        Assert.Equal(
            ["appmanifest_-4.acf", "appmanifest_.acf", "appmanifest_abc.acf"],
            skipped.Select(path => Path.GetFileName(path)).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Read_ManifestWithoutAppStateOrInstallDir_Skipped()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "\"Other\" { \"installdir\" \"G\" }");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf", "\"AppState\" { \"name\" \"X\" }");
        SteamTree.MakeGameFolder(temp.Folder, "G");
        var skipped = new List<string>();

        var apps = SteamTree.ReadAll(temp.Folder, skipped.Add);

        Assert.Equal(["G|G"], SteamTree.Pairs(apps));
        Assert.Equal(2, skipped.Count);
    }

    [Fact]
    public void Read_NoSkippedCallback_DoesNotThrow()
    {
        using var temp = new TempFolder();
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", "not vdf");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_x.acf", "not vdf");
        SteamTree.MakeGameFolder(temp.Folder, "G");

        Assert.Equal(["G|G"], SteamTree.Pairs(SteamTree.ReadAll(temp.Folder)));
    }

    [Fact]
    public void Read_ReadsAtMost2048Manifests()
    {
        using var temp = new TempFolder();
        SteamTree.MakeGameFolder(temp.Folder, "G");
        for (var id = 1; id <= 2049; id++)
        {
            SteamTree.WriteManifest(temp.Folder, FormattableString.Invariant($"appmanifest_{id}.acf"), "x");
        }

        var skipped = new List<string>();
        _ = SteamTree.ReadAll(temp.Folder, skipped.Add);

        Assert.Equal(2048, skipped.Count);
    }

    [Fact]
    public void Merge_KeepsFirstPerFolder()
    {
        IReadOnlyList<InstalledApp> first = [new("Steamworks Shared", "Steamworks Common Redistributables"), new("A", "A")];
        IReadOnlyList<InstalledApp> second = [new("steamworks shared", "Steamworks Shared"), new("B", "B")];

        var merged = InstalledApps.Merge([first, second]);

        Assert.Equal(["Steamworks Shared|Steamworks Common Redistributables", "A|A", "B|B"], SteamTree.Pairs(merged));
    }

    [Fact]
    public void Merge_Nothing_ReturnsEmpty()
    {
        Assert.Empty(InstalledApps.Merge([]));
    }

    [Fact]
    public void Read_StopsAfter4096Folders()
    {
        using var temp = new TempFolder();
        for (var id = 0; id < 4097; id++)
        {
            SteamTree.MakeGameFolder(temp.Folder, FormattableString.Invariant($"g{id}"));
        }

        Assert.Equal(4096, SteamTree.ReadAll(temp.Folder).Count);
    }

    [Fact]
    public void Read_ManifestPastTheBudget_IsSkippedAndTheFolderKeepsItsFolderName()
    {
        using var temp = new TempFolder();
        var first = SteamTree.Manifest("Alpha", "A");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", first);
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf", SteamTree.Manifest("Bravo", "B"));
        SteamTree.MakeGameFolder(temp.Folder, "A");
        SteamTree.MakeGameFolder(temp.Folder, "B");
        var skipped = new List<string>();

        var apps = SteamTree.ReadAll(temp.Folder, skipped.Add, budget: new ManifestBudget(first.Length));

        Assert.Equal(["A|Alpha", "B|B"], SteamTree.Pairs(apps));
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_2.acf")], skipped);
    }

    [Fact]
    public void ManifestBudget_ChargesAndNeverGoesBelowZero()
    {
        var budget = new ManifestBudget(10);

        budget.Charge(4);
        Assert.Equal(6, budget.Remaining);
        budget.Charge(7);
        Assert.Equal(0, budget.Remaining);
    }

    [Fact]
    public void Read_ChargesTheBytesActuallyRead_AndAFileOverTheRemainingBudgetIsSkipped()
    {
        using var temp = new TempFolder();
        var small = SteamTree.Manifest("Alpha", "A");
        SteamTree.WriteManifest(temp.Folder, "appmanifest_1.acf", small);
        SteamTree.WriteManifest(temp.Folder, "appmanifest_2.acf", SteamTree.Manifest("Bravo", "B") + new string(' ', 100));
        SteamTree.MakeGameFolder(temp.Folder, "A");
        SteamTree.MakeGameFolder(temp.Folder, "B");
        var skipped = new List<string>();
        var budget = new ManifestBudget(small.Length + 50);

        var apps = SteamTree.ReadAll(temp.Folder, skipped.Add, budget: budget);

        // The first file costs exactly its bytes; the second is over what is left, so it is skipped and takes only what the limit allowed.
        Assert.Equal(["A|Alpha", "B|B"], SteamTree.Pairs(apps));
        Assert.Equal([Path.Combine(temp.Folder, "steamapps", "appmanifest_2.acf")], skipped);
        Assert.Equal(0, budget.Remaining);
    }

    [Fact]
    public void MovedTargetIntoSystemFolder_IsRefused()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(SteamLayout.CommonFolder(temp.Folder));
        var moved = Path.Combine(temp.Folder, "moved", "Fine");
        Directory.CreateDirectory(moved);
        var toRoot = Path.Combine(SteamLayout.CommonFolder(temp.Folder), "ToRoot");
        var toSystem = Path.Combine(SteamLayout.CommonFolder(temp.Folder), "ToSystem");
        var toFine = Path.Combine(SteamLayout.CommonFolder(temp.Folder), "Fine");
        SteamTree.MakeJunction(toRoot, Path.GetPathRoot(Environment.SystemDirectory)!);
        SteamTree.MakeJunction(toSystem, Environment.SystemDirectory);
        SteamTree.MakeJunction(toFine, moved);
        var toProgramData = Path.Combine(SteamLayout.CommonFolder(temp.Folder), "ToProgramData");
        SteamTree.MakeJunction(toProgramData, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData));

        try
        {
            var apps = SteamTree.ReadAll(temp.Folder);

            Assert.Equal(["Fine|" + moved, "ToProgramData|", "ToRoot|", "ToSystem|"], apps.Select(app => app.Folder + "|" + app.MovedTo));
        }
        finally
        {
            Directory.Delete(toProgramData);
            Directory.Delete(toRoot);
            Directory.Delete(toSystem);
            Directory.Delete(toFine);
        }
    }

    [Fact]
    public void Read_JunctionedFolder_RecordsItsLocalTarget()
    {
        using var temp = new TempFolder();
        var moved = Path.Combine(temp.Folder, "moved", "Game");
        Directory.CreateDirectory(moved);
        Directory.CreateDirectory(SteamLayout.CommonFolder(temp.Folder));
        SteamTree.MakeGameFolder(temp.Folder, "Plain");
        var link = Path.Combine(SteamLayout.CommonFolder(temp.Folder), "Game");
        SteamTree.MakeJunction(link, moved);

        try
        {
            var local = SteamTree.ReadAll(temp.Folder, isLocalDrive: _ => true);
            var refused = SteamTree.ReadAll(temp.Folder, isLocalDrive: _ => false);

            Assert.Equal([moved, null], local.Select(app => app.MovedTo));
            Assert.All(refused, app => Assert.Null(app.MovedTo));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void MovedTargetIntoSystemFolder_FromADriveRootLibrary_IsRefused()
    {
        // A real drive-root library cannot be made in a test, so a temp folder stands in for the drive root and the policy roots
        // are supplied: the library root is that stand-in, and the system folder lies inside it, as C:\Windows lies inside C:\.
        using var temp = new TempFolder();
        var drive = temp.Folder;
        var system = Path.Combine(drive, "Win");
        var roots = new PolicyRoots(system, Path.Combine(drive, "Pf"), Path.Combine(drive, "Pf86"), Path.Combine(drive, "Pd"), Path.Combine(drive, "Pf86", "Steam", "steamapps", "common"));
        var fine = Path.Combine(drive, "Games", "Fine");
        var programData = Path.Combine(drive, "Pd", "Sub");
        Directory.CreateDirectory(Path.Combine(system, "System32"));
        Directory.CreateDirectory(programData);
        Directory.CreateDirectory(fine);
        Directory.CreateDirectory(SteamLayout.CommonFolder(drive));
        var toSystem = Path.Combine(SteamLayout.CommonFolder(drive), "ToSystem");
        var toFine = Path.Combine(SteamLayout.CommonFolder(drive), "Fine");
        SteamTree.MakeJunction(toSystem, Path.Combine(system, "System32"));
        SteamTree.MakeJunction(toFine, fine);
        var toProgramData = Path.Combine(SteamLayout.CommonFolder(drive), "ToProgramData");
        SteamTree.MakeJunction(toProgramData, programData);

        try
        {
            var apps = InstalledApps.Read(drive, _ => { }, Anywhere, new ManifestBudget(), roots);

            Assert.Equal(["Fine|" + fine, "ToProgramData|", "ToSystem|"], apps.Select(app => app.Folder + "|" + app.MovedTo));
        }
        finally
        {
            Directory.Delete(toProgramData);
            Directory.Delete(toSystem);
            Directory.Delete(toFine);
        }
    }

    [Fact]
    public void Read_CommonFolderThatIsALinkToARefusedTarget_ReturnsEmptyWithoutListingIt()
    {
        using var temp = new TempFolder();
        var offDrive = Path.Combine(temp.Folder, "offdrive", "common");
        Directory.CreateDirectory(Path.Combine(offDrive, "Hidden"));
        Directory.CreateDirectory(SteamLayout.SteamAppsFolder(temp.Folder));
        var link = SteamLayout.CommonFolder(temp.Folder);
        SteamTree.MakeJunction(link, offDrive);

        try
        {
            var apps = SteamTree.ReadAll(temp.Folder, isLocalDrive: path => !path.Contains("offdrive", StringComparison.Ordinal));
            var allowed = SteamTree.ReadAll(temp.Folder);

            Assert.Empty(apps);
            Assert.Equal(["Hidden|Hidden"], SteamTree.Pairs(allowed));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void Read_SteamappsThatIsALinkToARefusedTarget_ReadsNothing()
    {
        using var temp = new TempFolder();
        var offDrive = Path.Combine(temp.Folder, "offdrive", "steamapps");
        Directory.CreateDirectory(Path.Combine(offDrive, "common", "Hidden"));
        File.WriteAllText(Path.Combine(offDrive, "appmanifest_1.acf"), SteamTree.Manifest("Secret", "Hidden"));
        var link = SteamLayout.SteamAppsFolder(temp.Folder);
        SteamTree.MakeJunction(link, offDrive);
        var skipped = new List<string>();

        try
        {
            var apps = SteamTree.ReadAll(temp.Folder, skipped.Add, path => !path.Contains("offdrive", StringComparison.Ordinal));

            Assert.Empty(apps);
            Assert.Empty(skipped);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    [Fact]
    public void Read_ManifestThatIsALinkToARefusedTarget_IsSkippedUnread()
    {
        using var temp = new TempFolder();
        var offDrive = Path.Combine(temp.Folder, "offdrive");
        Directory.CreateDirectory(offDrive);
        var real = Path.Combine(offDrive, "real.acf");
        File.WriteAllText(real, SteamTree.Manifest("Secret", "G"));
        SteamTree.MakeGameFolder(temp.Folder, "G");
        var link = Path.Combine(SteamLayout.SteamAppsFolder(temp.Folder), "appmanifest_1.acf");
        try
        {
            File.CreateSymbolicLink(link, real);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            // A file symbolic link needs the symbolic-link right (admin or Developer Mode); the folder-link tests above cover the same rule.
            return;
        }

        try
        {
            var skipped = new List<string>();
            var apps = SteamTree.ReadAll(temp.Folder, skipped.Add, path => !path.Contains("offdrive", StringComparison.Ordinal));

            Assert.Equal(["G|G"], SteamTree.Pairs(apps));
            Assert.Equal([link], skipped);
        }
        finally
        {
            File.Delete(link);
        }
    }
}
