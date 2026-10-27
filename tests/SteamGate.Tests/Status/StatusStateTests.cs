using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Core.Steam;
using SteamGate.Tests.Rules;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusStateTests
{
    private static readonly DateTime Tuesday = new(2026, 10, 20, 21, 14, 0);

    private static readonly InstalledApp[] Installed =
    [
        new("Cyberpunk 2077", "Cyberpunk 2077"),
        new("Viewfinder", "Viewfinder"),
        new("Blender", "Blender"),
        new("wallpaper_engine", "Wallpaper Engine"),
    ];

    private static GateSnapshot Snapshot() => new([new RunningGame("Cyberpunk 2077", "Cyberpunk 2077", [])], Installed)
    {
        Tally = new Tally(new DateOnly(2026, 10, 20), 2820, new DateOnly(2026, 10, 19), 22200, null),
        RemainingSeconds = 2580,
        TonightLimitMinutes = 90,
        HelperConnected = true,
        Settings = GateSettings.Defaults,
    };

    [Fact]
    public void FromSnapshot()
    {
        var state = StatusState.From(Snapshot(), Tuesday)!;

        Assert.Equal("Cyberpunk 2077", state.Counting);
        Assert.Equal(2820, state.NightSeconds);
        Assert.Equal(22200, state.WeekSeconds);
        Assert.False(state.TonightWeekend);
        Assert.Equal(2580, state.RemainingSeconds);
        Assert.Equal(90, state.TonightLimitMinutes);
        Assert.True(state.HelperConnected);
        Assert.Equal(["Cyberpunk 2077", "Viewfinder"], state.Addable.Select(entry => entry.DisplayName));
        Assert.Equal(["Cyberpunk 2077", "Viewfinder"], state.Addable.Select(entry => entry.Folder));
        Assert.Equal(["Blender", "OBS Studio", "Wallpaper Engine", "Steamworks Shared"], state.Settings.NotCounted.Select(entry => entry.DisplayName));
        Assert.Equal(["Blender", "OBS Studio", "wallpaper_engine", "Steamworks Shared"], state.Settings.NotCounted.Select(entry => entry.Folder));
        Assert.Equal(90, state.Settings.WeeknightMinutes);
        Assert.Equal(180, state.Settings.WeekendNightMinutes);
        Assert.Equal(600, state.Settings.WeeklyCapMinutes);
        Assert.Equal(48, state.Settings.CooldownHours);

        Assert.Equal("Wallpaper Engine", StatusState.DisplayName("wallpaper_engine", []));
        Assert.Equal("Viewfinder", StatusState.DisplayName("VIEWFINDER", Installed));
        Assert.Equal("Gone Game", StatusState.DisplayName("Gone Game", []));
        Assert.Equal("Wallpaper Engine", StatusState.DisplayName("Wallpaper_Engine", Installed));

        var friday = StatusState.From(Snapshot(), new DateTime(2026, 10, 23, 21, 14, 0))!;
        Assert.True(friday.TonightWeekend);
        Assert.Equal(0, friday.NightSeconds);
        Assert.Equal(22200, friday.WeekSeconds);

        var earlySaturday = StatusState.From(Snapshot(), new DateTime(2026, 10, 24, 2, 0, 0))!;
        Assert.True(earlySaturday.TonightWeekend);

        var newWeek = StatusState.From(Snapshot(), new DateTime(2026, 10, 26, 21, 14, 0))!;
        Assert.Equal(0, newWeek.WeekSeconds);
        Assert.False(newWeek.TonightWeekend);

        Assert.Null(StatusState.From(Snapshot() with { Games = [] }, Tuesday)!.Counting);
    }

    [Fact]
    public void NullUntilReady()
    {
        Assert.Null(StatusState.From(Snapshot() with { Settings = null }, Tuesday));
        Assert.Null(StatusState.From(Snapshot() with { RemainingSeconds = null }, Tuesday));
        Assert.Null(StatusState.From(GateSnapshot.Empty, Tuesday));
    }

    [Fact]
    public void RemovedApprovedProgramsCanBeAddedBack()
    {
        var settings = GateSettings.Defaults with { NotCounted = ["Blender"] };

        var state = StatusState.From(Snapshot() with { Settings = settings }, Tuesday)!;

        Assert.Equal(["Cyberpunk 2077", "Viewfinder", "Wallpaper Engine"], state.Addable.Select(entry => entry.DisplayName));
        Assert.Equal(["Blender"], state.Settings.NotCounted.Select(entry => entry.Folder));
    }

    [Fact]
    public void AddableIsSortedByNameThenFolderAndSkipsListedFoldersWhateverTheCase()
    {
        InstalledApp[] apps = [new("b2", "beta"), new("a1", "Alpha"), new("b1", "beta"), new("LISTED", "Listed")];
        var settings = GateSettings.Defaults with { NotCounted = ["listed"] };

        var state = StatusState.From(new GateSnapshot([], apps) { RemainingSeconds = 1, Settings = settings }, Tuesday)!;

        Assert.Equal(["a1", "b1", "b2"], state.Addable.Select(entry => entry.Folder));
    }

    [Fact]
    public void NameCutKeepsWholeCharacters()
    {
        var emoji = "\U0001F600";

        Assert.Equal(new string('a', 127), StatusState.DisplayName("f", [new InstalledApp("f", new string('a', 127) + emoji)]));
        Assert.Equal(new string('a', 128), StatusState.DisplayName("f", [new InstalledApp("f", new string('a', 200))]));
        var whole = new string('a', 126) + emoji;
        Assert.Equal(128, whole.Length);
        Assert.Equal(whole, StatusState.DisplayName("f", [new InstalledApp("f", whole)]));
        Assert.Equal(new string('a', 128), StatusState.DisplayName("f", [new InstalledApp("f", new string('a', 128))]));
        Assert.Equal(new string('b', 128), StatusState.DisplayName(new string('b', 300), []));
        Assert.Equal(128, StatusState.MaxNameLength);
        Assert.Equal(512, StatusState.MaxAddable);
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var pending = new PendingChange[]
        {
            new(RuleFixtures.IdNumber(1), ChangeFields.WeekendNightsAdd, null, null, null, [DayOfWeek.Sunday], new DateTime(2026, 10, 20, 21, 14, 0, DateTimeKind.Utc), 100),
            new(RuleFixtures.IdNumber(2), ChangeFields.NotCountedAdd, null, null, "Viewfinder", null, new DateTime(2026, 10, 20, 21, 15, 0, DateTimeKind.Utc), 200),
        };
        var original = new GateSettings
        {
            WeeknightMinutes = 75,
            CooldownHours = 24,
            WeekendNights = [DayOfWeek.Saturday],
            Enabled = false,
            AllowUninstall = true,
            NotCounted = ["Blender"],
            Pending = pending,
            Clock = new LockClock(DateTime.UtcNow, 5, Guid.NewGuid()),
        };
        var snapshot = Snapshot() with { Settings = original };

        var rebuilt = StatusState.ToSettings(StatusState.From(snapshot, Tuesday)!);

        Assert.Equal(75, rebuilt.WeeknightMinutes);
        Assert.Equal(180, rebuilt.WeekendNightMinutes);
        Assert.Equal(600, rebuilt.WeeklyCapMinutes);
        Assert.Equal(24, rebuilt.CooldownHours);
        Assert.Equal([DayOfWeek.Saturday], rebuilt.WeekendNights);
        Assert.False(rebuilt.Enabled);
        Assert.True(rebuilt.AllowUninstall);
        Assert.Equal(["Blender"], rebuilt.NotCounted);
        Assert.Equal(2, rebuilt.Pending.Count);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(pending[index].Id, rebuilt.Pending[index].Id);
            Assert.Equal(pending[index].Field, rebuilt.Pending[index].Field);
            Assert.Equal(pending[index].Number, rebuilt.Pending[index].Number);
            Assert.Equal(pending[index].Flag, rebuilt.Pending[index].Flag);
            Assert.Equal(pending[index].Folder, rebuilt.Pending[index].Folder);
            Assert.Equal(pending[index].Days, rebuilt.Pending[index].Days);
            Assert.Equal(pending[index].RequestedUtc, rebuilt.Pending[index].RequestedUtc);
            Assert.Equal(pending[index].RemainingWaitSeconds, rebuilt.Pending[index].RemainingWaitSeconds);
        }

        Assert.Null(rebuilt.Clock);
    }

    [Fact]
    public void StateNamesComeFromTheAddListAndTheNotCountedList()
    {
        var state = StateFixtures.State(addable: [new NamedFolder("Viewfinder", "Viewfinder Deluxe")]);

        Assert.Equal("Viewfinder Deluxe", StatusState.DisplayName("VIEWFINDER", state));
        Assert.Equal("Wallpaper Engine", StatusState.DisplayName("wallpaper_engine", state));
        Assert.Equal("OBS Studio", StatusState.DisplayName("obs studio", state));
        Assert.Equal("Gone Game", StatusState.DisplayName("Gone Game", state));
        Assert.Equal(new string((char)98, 128), StatusState.DisplayName(new string((char)98, 300), state));
    }

    [Fact]
    public void AFolderThatIsNotWellFormedTextIsNeverOffered()
    {
        var pair = "pair😀";
        InstalledApp[] apps = [new("ok", "Fine"), new("bad\uD800", "Lone high"), new("bad\uDC00x", "Lone low"), new(pair, "Pair")];

        var state = StatusState.From(new GateSnapshot([], apps) { RemainingSeconds = 1, Settings = GateSettings.Defaults }, Tuesday)!;

        Assert.Equal(["ok", pair], state.Addable.Select(entry => entry.Folder));
        Assert.NotNull(PipeMessage.Parse(state.Serialize()));
    }

    [Fact]
    public void ExtensionMinutesFromTally()
    {
        var withExtension = Snapshot() with
        {
            TonightLimitMinutes = 110,
            Tally = new Tally(new DateOnly(2026, 10, 20), 2820, new DateOnly(2026, 10, 19), 22200, null, new NightExtension(new DateOnly(2026, 10, 20), 20)),
        };

        var state = StatusState.From(withExtension, Tuesday)!;
        Assert.Equal(20, state.ExtensionMinutes);
        Assert.Equal(110, state.TonightLimitMinutes);

        var nextNight = StatusState.From(withExtension, new DateTime(2026, 10, 21, 21, 14, 0))!;
        Assert.Equal(0, nextNight.ExtensionMinutes);
        Assert.Equal(0, nextNight.NightSeconds);

        Assert.Equal(0, StatusState.From(Snapshot(), Tuesday)!.ExtensionMinutes);
    }
}
