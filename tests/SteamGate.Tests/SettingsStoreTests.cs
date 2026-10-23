using System.Text.RegularExpressions;
using SteamGate.Core;
using SteamGate.Core.Rules;
using Xunit;

namespace SteamGate.Tests;

public sealed class SettingsStoreTests
{
    private static readonly string[] DefaultNotCounted = ["Blender", "OBS Studio", "wallpaper_engine", "Steamworks Shared"];
    private static readonly DayOfWeek[] DefaultWeekendNights = [DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
    private static readonly DayOfWeek[] OnlyFriday = [DayOfWeek.Friday];
    private static readonly string[] OnlyBlender = ["Blender"];
    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromMilliseconds(200);

    private static readonly GateSettings Customized = GateSettings.Defaults with
    {
        WeeknightMinutes = 75,
        WeekendNightMinutes = 200,
        WeekendNights = [DayOfWeek.Friday],
        WeeklyCapMinutes = 500,
        CooldownHours = 24,
        NotCounted = ["Blender"],
        Enabled = false,
    };

    [Fact]
    public void Load_EmptyFolder_WritesDefaults()
    {
        using var temp = new TempFolder();

        var result = new SettingsStore(temp.Folder).Load();

        AssertDefaults(result.Value);
        Assert.False(result.Corrupt);
        Assert.True(File.Exists(temp.PathOf("settings.json")));
        var compact = WithoutWhitespace(File.ReadAllText(temp.PathOf("settings.json")));
        Assert.Contains("\"weeknightMinutes\":90", compact, StringComparison.Ordinal);
        Assert.Contains("\"weekendNights\":[\"Friday\",\"Saturday\",\"Sunday\"]", compact, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();

        Assert.True(store.Save(Customized));
        Assert.True(store.Save(Customized with { WeeknightMinutes = 80 }));
        var loaded = new SettingsStore(temp.Folder).Load();

        Assert.False(loaded.Corrupt);
        Assert.Equal(80, loaded.Value.WeeknightMinutes);
        Assert.Equal(200, loaded.Value.WeekendNightMinutes);
        Assert.Equal(OnlyFriday, loaded.Value.WeekendNights);
        Assert.Equal(500, loaded.Value.WeeklyCapMinutes);
        Assert.Equal(24, loaded.Value.CooldownHours);
        Assert.Equal(OnlyBlender, loaded.Value.NotCounted);
        Assert.False(loaded.Value.Enabled);
        Assert.False(File.Exists(temp.PathOf("settings.json.tmp")));
    }

    [Fact]
    public void Load_MissingFields_TakeDefaults()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), "{\"weeknightMinutes\":75}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.Equal(75, result.Value.WeeknightMinutes);
        Assert.True(result.Value.Enabled);
        Assert.Equal(48, result.Value.CooldownHours);
        Assert.Equal(600, result.Value.WeeklyCapMinutes);
        Assert.Equal(180, result.Value.WeekendNightMinutes);
        Assert.Equal(DefaultWeekendNights, result.Value.WeekendNights);
        Assert.Equal(DefaultNotCounted, result.Value.NotCounted);
        Assert.False(result.Corrupt);
    }

    [Fact]
    public void Load_UnknownProperty_IsIgnored()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), "{\"weeknightMinutes\":75,\"mystery\":[1,2]}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.Equal(75, result.Value.WeeknightMinutes);
        Assert.False(result.Corrupt);
    }

    [Fact]
    public void Load_Corrupt_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("not json");
    }

    [Fact]
    public void Load_Null_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("null");
    }

    [Fact]
    public void Load_NullField_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("{\"weekendNights\":null}");
    }

    [Fact]
    public void Load_NullItem_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("{\"notCounted\":[null]}");
    }

    [Fact]
    public void Load_MixedNullItem_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("{\"notCounted\":[\"Blender\",null]}");
    }

    [Fact]
    public void Save_NullList_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(temp.PathOf("settings.json"));

        Assert.False(store.Save(GateSettings.Defaults with { NotCounted = null! }));
        Assert.False(store.Save(GateSettings.Defaults with { WeekendNights = null! }));

        Assert.Equal(before, File.ReadAllText(temp.PathOf("settings.json")));
        Assert.False(new SettingsStore(temp.Folder).Load().Corrupt);
    }

    [Fact]
    public void Save_NullItem_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(temp.PathOf("settings.json"));

        Assert.False(store.Save(GateSettings.Defaults with { NotCounted = ["Blender", null!] }));

        Assert.Equal(before, File.ReadAllText(temp.PathOf("settings.json")));
        Assert.False(new SettingsStore(temp.Folder).Load().Corrupt);
    }

    [Theory]
    [InlineData("weeknightMinutes")]
    [InlineData("weekendNightMinutes")]
    [InlineData("weeklyCapMinutes")]
    [InlineData("cooldownHours")]
    public void Load_NegativeNumber_FallsBackToDefaults(string field)
    {
        AssertFallsBackToDefaults("{\"" + field + "\":-1}");
    }

    [Fact]
    public void Save_NegativeNumber_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(temp.PathOf("settings.json"));

        Assert.False(store.Save(GateSettings.Defaults with { CooldownHours = -1 }));

        Assert.Equal(before, File.ReadAllText(temp.PathOf("settings.json")));
    }

    [Fact]
    public void Save_Rejected_ReturnsFalse()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();

        Assert.False(store.Save(GateSettings.Defaults with { WeeknightMinutes = -5 }));

        Assert.Contains("\"weeknightMinutes\":90", WithoutWhitespace(File.ReadAllText(temp.PathOf("settings.json"))), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ZeroNumbers_AreKept()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), "{\"weeknightMinutes\":0,\"weekendNightMinutes\":0,\"weeklyCapMinutes\":0,\"cooldownHours\":0}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.False(result.Corrupt);
        Assert.Equal(0, result.Value.WeeknightMinutes);
        Assert.Equal(0, result.Value.WeekendNightMinutes);
        Assert.Equal(0, result.Value.WeeklyCapMinutes);
        Assert.Equal(0, result.Value.CooldownHours);
    }

    [Fact]
    public void Load_Locked_RethrowsWithoutOverwrite()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        File.WriteAllText(path, "{\"weeknightMinutes\":75}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.ThrowsAny<IOException>(() => new SettingsStore(temp.Folder).Load());

        held.Dispose();
        Assert.Equal("{\"weeknightMinutes\":75}", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(temp.Folder, "settings.corrupt-*"));
    }

    [Fact]
    public void Load_LockedThenReleased_RetriesAndReads()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        File.WriteAllText(path, "{\"weeknightMinutes\":75}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        using var releaser = ReleaseAfterDelay(held);

        var result = new SettingsStore(temp.Folder).Load();

        Assert.Equal(75, result.Value.WeeknightMinutes);
        Assert.False(result.Corrupt);
    }

    [Fact]
    public void Load_IntegerDay_FallsBackToDefaults()
    {
        AssertFallsBackToDefaults("{\"weekendNights\":[7]}");
    }

    [Fact]
    public void Save_TempPathBlocked_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        Directory.CreateDirectory(temp.PathOf("settings.json.tmp"));

        var saved = store.Save(GateSettings.Defaults with { WeeknightMinutes = 80 });

        Assert.False(saved);
        Assert.Equal(90, new SettingsStore(temp.Folder).Load().Value.WeeknightMinutes);
    }

    [Fact]
    public void Load_LockedForever_WaitsThroughAllRetries()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        File.WriteAllText(path, "{\"weeknightMinutes\":75}");
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        Assert.ThrowsAny<IOException>(() => new SettingsStore(temp.Folder).Load());

        Assert.True(clock.Elapsed >= JsonFileStore<GateSettings>.GiveUpAfter - ReleaseDelay, $"gave up after {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Save_LockedForever_WaitsThroughAllRetries()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        var store = new SettingsStore(temp.Folder);
        store.Load();
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        var saved = store.Save(GateSettings.Defaults with { WeeknightMinutes = 80 });

        Assert.False(saved);
        Assert.True(clock.Elapsed >= JsonFileStore<GateSettings>.GiveUpAfter - ReleaseDelay, $"gave up after {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Load_FolderMissing_ThrowsAndDoesNotCreateIt()
    {
        using var temp = new TempFolder();
        var missing = Path.Combine(temp.Folder, "not-created");

        Assert.ThrowsAny<IOException>(() => new SettingsStore(missing).Load());

        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void Save_Locked_ReturnsFalseWithoutThrow()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(path);
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var saved = store.Save(GateSettings.Defaults with { WeeknightMinutes = 80 });

        held.Dispose();
        Assert.False(saved);
        Assert.Equal(before, File.ReadAllText(path));
        Assert.False(File.Exists(temp.PathOf("settings.json.tmp")));
    }

    [Fact]
    public void Save_Null_ReturnsFalse()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(path);

        var saved = store.Save(null!);

        Assert.False(saved);
        Assert.Equal(before, File.ReadAllText(path));
        Assert.False(File.Exists(temp.PathOf("settings.json.tmp")));
    }

    [Fact]
    public void Save_LockedThenReleased_RetriesAndSaves()
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        var store = new SettingsStore(temp.Folder);
        store.Load();
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        using var releaser = ReleaseAfterDelay(held);

        var saved = store.Save(GateSettings.Defaults with { WeeknightMinutes = 80 });

        Assert.True(saved);
        Assert.Equal(80, new SettingsStore(temp.Folder).Load().Value.WeeknightMinutes);
        Assert.False(File.Exists(temp.PathOf("settings.json.tmp")));
    }

    [Fact]
    public void Save_FolderMissing_ReturnsFalse()
    {
        using var temp = new TempFolder();
        var missing = Path.Combine(temp.Folder, "not-created");

        var saved = new SettingsStore(missing).Save(GateSettings.Defaults);

        Assert.False(saved);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void PendingRoundTrips()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var requested = new DateTime(2026, 10, 20, 21, 0, 0, 123, DateTimeKind.Utc);
        var saved = GateSettings.Defaults with
        {
            Pending =
            [
                new PendingChange(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"), ChangeFields.WeekendNightsAdd, null, null, null, [DayOfWeek.Sunday], requested, 172799.5),
                new PendingChange(Guid.Parse("1f8fad5b-d9cb-469f-a165-70867728950e"), ChangeFields.AllowUninstall, null, true, null, null, requested.AddMinutes(1), 12.25),
                new PendingChange(Guid.Parse("2f8fad5b-d9cb-469f-a165-70867728950e"), ChangeFields.NotCountedAdd, null, null, "Viewfinder", null, requested, 1),
                new PendingChange(Guid.Parse("3f8fad5b-d9cb-469f-a165-70867728950e"), ChangeFields.WeeknightMinutes, 120, null, null, null, requested, 0),
            ],
            AllowUninstall = true,
            Clock = new LockClock(new DateTime(2026, 10, 20, 22, 30, 0, DateTimeKind.Utc), 123456789012, Guid.Parse("9f8fad5b-d9cb-469f-a165-70867728950e")),
        };

        Assert.True(store.Save(saved));
        var loaded = new SettingsStore(temp.Folder).Load();

        Assert.False(loaded.Corrupt);
        Assert.True(loaded.Value.AllowUninstall);
        Assert.Equal(saved.Pending.Count, loaded.Value.Pending.Count);
        for (var index = 0; index < saved.Pending.Count; index++)
        {
            var before = saved.Pending[index];
            var after = loaded.Value.Pending[index];
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.Field, after.Field);
            Assert.Equal(before.Number, after.Number);
            Assert.Equal(before.Flag, after.Flag);
            Assert.Equal(before.Folder, after.Folder);
            Assert.Equal(before.RequestedUtc, after.RequestedUtc);
            Assert.Equal(before.RemainingWaitSeconds, after.RemainingWaitSeconds);
            Assert.Equal(before.Days is null, after.Days is null);
            if (before.Days is not null)
            {
                Assert.Equal(before.Days, after.Days);
            }
        }

        Assert.Equal(saved.Clock!.LastSavedUtc, loaded.Value.Clock!.LastSavedUtc);
        Assert.Equal(saved.Clock.LastSavedUptimeMs, loaded.Value.Clock.LastSavedUptimeMs);
        Assert.Equal(saved.Clock.BootId, loaded.Value.Clock.BootId);
    }

    [Fact]
    public void Load_NoLockFields_TakeDefaults()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), "{\"weeknightMinutes\":75}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.False(result.Corrupt);
        Assert.Empty(result.Value.Pending);
        Assert.False(result.Value.AllowUninstall);
        Assert.Null(result.Value.Clock);
    }

    [Theory]
    [InlineData("{\"pending\":[null]}")]
    [InlineData("{\"pending\":null}")]
    [InlineData("{\"pending\":[{\"id\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"field\":\"bogus\",\"number\":5,\"requestedUtc\":\"2026-10-20T21:00:00Z\",\"remainingWaitSeconds\":10}]}")]
    [InlineData("{\"pending\":[{\"id\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"field\":\"weeknightMinutes\",\"flag\":true,\"requestedUtc\":\"2026-10-20T21:00:00Z\",\"remainingWaitSeconds\":10}]}")]
    [InlineData("{\"pending\":[{\"id\":\"0f8fad5b-d9cb-469f-a165-70867728950e\",\"field\":\"weeknightMinutes\",\"number\":1441,\"requestedUtc\":\"2026-10-20T21:00:00Z\",\"remainingWaitSeconds\":10}]}")]
    [InlineData("{\"cooldownHours\":169}")]
    [InlineData("{\"weeknightMinutes\":1441}")]
    [InlineData("{\"weekendNightMinutes\":1441}")]
    [InlineData("{\"weeklyCapMinutes\":10081}")]
    public void Load_OutOfRangeOrBadPending_FallsBackToDefaults(string content)
    {
        AssertFallsBackToDefaults(content);
    }

    [Fact]
    public void Load_ValuesAtTheTop_AreKept()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), "{\"cooldownHours\":168,\"weeknightMinutes\":1440,\"weekendNightMinutes\":1440,\"weeklyCapMinutes\":10080}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.False(result.Corrupt);
        Assert.Equal(168, result.Value.CooldownHours);
        Assert.Equal(1440, result.Value.WeeknightMinutes);
        Assert.Equal(1440, result.Value.WeekendNightMinutes);
        Assert.Equal(10080, result.Value.WeeklyCapMinutes);
    }

    [Fact]
    public void Load_FiftyFolders_AreKeptAndFiftyOneAreCorrupt()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var fifty = GateSettings.Defaults with { NotCounted = Enumerable.Range(0, 50).Select(n => "F" + n).ToArray() };
        var fiftyOne = GateSettings.Defaults with { NotCounted = Enumerable.Range(0, 51).Select(n => "F" + n).ToArray() };

        Assert.True(store.Save(fifty));
        Assert.False(store.Save(fiftyOne));

        Assert.Equal(50, new SettingsStore(temp.Folder).Load().Value.NotCounted.Count);
    }

    [Fact]
    public void Save_BadValues_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(temp.PathOf("settings.json"));
        var bad = new PendingChange(Guid.NewGuid(), "bogus", 1, null, null, null, DateTime.UtcNow, 5);

        Assert.False(store.Save(GateSettings.Defaults with { CooldownHours = 169 }));
        Assert.False(store.Save(GateSettings.Defaults with { WeeknightMinutes = 1441 }));
        Assert.False(store.Save(GateSettings.Defaults with { WeekendNightMinutes = 1441 }));
        Assert.False(store.Save(GateSettings.Defaults with { WeeklyCapMinutes = 10081 }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [bad] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [null!] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = null! }));

        Assert.Equal(before, File.ReadAllText(temp.PathOf("settings.json")));
    }

    // Releases the lock from a timer one retry delay after it starts; the store retries for GiveUpAfter, so the release lands well inside the window and the tests assert only the outcome.
    private static Timer ReleaseAfterDelay(FileStream held) => new(_ => held.Dispose(), null, ReleaseDelay, Timeout.InfiniteTimeSpan);

    private static void AssertFallsBackToDefaults(string badContent)
    {
        using var temp = new TempFolder();
        var path = temp.PathOf("settings.json");
        File.WriteAllText(path, badContent);

        var result = new SettingsStore(temp.Folder).Load();

        AssertDefaults(result.Value);
        Assert.True(result.Corrupt);
        var kept = Assert.Single(Directory.GetFiles(temp.Folder, "settings.corrupt-*"));
        Assert.Matches(new Regex(@"^settings\.corrupt-\d{8}T\d{9}Z\.json$"), Path.GetFileName(kept));
        Assert.Equal(badContent, File.ReadAllText(kept));
        Assert.Contains("\"weeknightMinutes\":90", WithoutWhitespace(File.ReadAllText(path)), StringComparison.Ordinal);
        var rewritten = new SettingsStore(temp.Folder).Load();
        AssertDefaults(rewritten.Value);
        Assert.False(rewritten.Corrupt);
    }

    private static void AssertDefaults(GateSettings settings)
    {
        Assert.Equal(90, settings.WeeknightMinutes);
        Assert.Equal(180, settings.WeekendNightMinutes);
        Assert.Equal(DefaultWeekendNights, settings.WeekendNights);
        Assert.Equal(600, settings.WeeklyCapMinutes);
        Assert.Equal(48, settings.CooldownHours);
        Assert.Equal(DefaultNotCounted, settings.NotCounted);
        Assert.True(settings.Enabled);
    }

    private static string WithoutWhitespace(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
