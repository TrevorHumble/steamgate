using System.Text;
using SteamGate.Core;
using SteamGate.Core.Accounting;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StateMessageTests
{
    private static readonly Guid PendingId = new("11111111-2222-3333-4444-555555555555");

    private static PendingChange WeeknightEntry() =>
        new(PendingId, ChangeFields.WeeknightMinutes, 120, null, null, null, new DateTime(2026, 10, 20, 21, 14, 0, DateTimeKind.Utc), 164880);

    [Fact]
    public void RoundTrips()
    {
        var state = StateFixtures.State(StateFixtures.WithPending(WeeknightEntry()), addable: [StateFixtures.Named("Viewfinder")]);

        var line = Encoding.UTF8.GetString(state.ToLine());
        var parsed = Assert.IsType<StateMessage>(PipeMessage.Parse(line.TrimEnd('\n')));

        Assert.Equal(state, parsed);
        Assert.Equal(state.Serialize(), parsed.Serialize());
        Assert.EndsWith("\n", line, StringComparison.Ordinal);
        Assert.Equal(1, line.Count(character => character == '\n'));
        Assert.Equal(2580, parsed.RemainingSeconds);
        Assert.Equal(90, parsed.TonightLimitMinutes);
        Assert.Equal("Cyberpunk 2077", parsed.Counting);
        Assert.Equal(164880, Assert.Single(parsed.Settings.Pending).RemainingWaitSeconds);
        Assert.Equal("Viewfinder", Assert.Single(parsed.Addable).DisplayName);
        Assert.Equal(["Blender", "OBS Studio", "Wallpaper Engine", "Steamworks Shared"], parsed.Settings.NotCounted.Select(entry => entry.DisplayName));

        // The form with nothing to say: null time left, null limit, no counted game.
        const string quiet = """{"type":"state","remainingSeconds":null,"tonightLimitMinutes":null,"tonightWeekend":false,"nightSeconds":0,"weekSeconds":0,"counting":null,"helperConnected":false,"settings":{"weeknightMinutes":90,"weekendNightMinutes":180,"weeklyCapMinutes":600,"cooldownHours":48,"enabled":true,"allowUninstall":false,"weekendNights":["Friday"],"notCounted":[],"pending":[]},"addable":[]}""";
        var quietState = Assert.IsType<StateMessage>(PipeMessage.Parse(quiet));
        Assert.Null(quietState.RemainingSeconds);
        Assert.Equal([DayOfWeek.Friday], quietState.Settings.WeekendNights);
    }

    [Fact]
    public void EveryPendingMemberRoundTrips()
    {
        var requested = new DateTime(2026, 10, 20, 21, 14, 5, DateTimeKind.Utc);
        PendingChange[] entries =
        [
            new(Guid.Parse("00000000-0000-0000-0000-000000000001"), ChangeFields.Enabled, null, false, null, null, requested, 10.5),
            new(Guid.Parse("00000000-0000-0000-0000-000000000002"), ChangeFields.NotCountedAdd, null, null, "Viewfinder", null, requested, 0),
            new(Guid.Parse("00000000-0000-0000-0000-000000000003"), ChangeFields.WeekendNightsAdd, null, null, null, [DayOfWeek.Thursday, DayOfWeek.Monday], requested, 3600),
        ];
        var state = StateFixtures.State(GateSettings.Defaults with { Pending = entries, Enabled = false, AllowUninstall = true, WeekendNights = [DayOfWeek.Saturday] });

        var parsed = Assert.IsType<StateMessage>(PipeMessage.Parse(state.Serialize()));

        Assert.Equal(state, parsed);
        Assert.Equal(state.Serialize(), parsed.Serialize());
        Assert.False(parsed.Settings.Enabled);
        Assert.True(parsed.Settings.AllowUninstall);
        Assert.Equal([DayOfWeek.Saturday], parsed.Settings.WeekendNights);
        Assert.False(parsed.Settings.Pending[0].Flag);
        Assert.Equal("Viewfinder", parsed.Settings.Pending[1].Folder);
        Assert.Equal([DayOfWeek.Thursday, DayOfWeek.Monday], parsed.Settings.Pending[2].Days);
        Assert.Equal(requested, parsed.Settings.Pending[0].RequestedUtc);
    }

    [Fact]
    public void ParsesTheNullForms()
    {
        const string line = """{"type":"state","remainingSeconds":null,"tonightLimitMinutes":null,"tonightWeekend":false,"nightSeconds":0,"weekSeconds":0,"counting":null,"helperConnected":false,"settings":{"weeknightMinutes":90,"weekendNightMinutes":180,"weeklyCapMinutes":600,"cooldownHours":48,"enabled":true,"allowUninstall":false,"weekendNights":["Friday"],"notCounted":[],"pending":[]},"addable":[]}""";

        var state = Assert.IsType<StateMessage>(PipeMessage.Parse(line));
        Assert.Equal(0, state.ExtensionMinutes);

        Assert.Null(state.RemainingSeconds);
        Assert.Null(state.TonightLimitMinutes);
        Assert.Null(state.Counting);
        Assert.Equal([DayOfWeek.Friday], state.Settings.WeekendNights);
        Assert.Equal(line.Replace("\"tonightWeekend\":false,", "\"tonightWeekend\":false,\"extensionMinutes\":0,", StringComparison.Ordinal), state.Serialize());
    }

    [Fact]
    public void StatesCompareByValueOverEveryMember()
    {
        StateMessage Make(GateSettings? settings = null, string folder = "A", double night = 2820, string? counting = "Cyberpunk 2077", bool weekend = false, int? limit = 90) =>
            StateFixtures.State(settings, addable: [StateFixtures.Named(folder)], nightSeconds: night, counting: counting, weekend: weekend, tonightLimit: limit);

        var baseline = Make(StateFixtures.WithPending(WeeknightEntry()));
        var same = Make(StateFixtures.WithPending(WeeknightEntry()));

        Assert.Equal(baseline, same);
        Assert.Equal(baseline.GetHashCode(), same.GetHashCode());
        Assert.Equal(baseline.Settings, same.Settings);
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry()), folder: "B"));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry()), night: 2821));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry()), counting: null));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry()), weekend: true));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry()), limit: null));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry() with { RemainingWaitSeconds = 1 })));
        Assert.NotEqual(baseline, Make(StateFixtures.WithPending(WeeknightEntry() with { Days = [DayOfWeek.Monday] })));
        Assert.NotEqual(baseline, Make());
        Assert.NotEqual(baseline, Make(GateSettings.Defaults with { Pending = [WeeknightEntry()], CooldownHours = 24 }));
        Assert.NotEqual(baseline, Make(GateSettings.Defaults with { Pending = [WeeknightEntry()], NotCounted = ["Blender"] }));
        Assert.NotEqual(baseline, Make(GateSettings.Defaults with { Pending = [WeeknightEntry()], WeekendNights = [DayOfWeek.Friday] }));
        Assert.NotEqual(baseline, Make(GateSettings.Defaults with { Pending = [WeeknightEntry()], Enabled = false }));
        Assert.NotEqual(baseline, Make(GateSettings.Defaults with { Pending = [WeeknightEntry()], AllowUninstall = true }));
        Assert.NotEqual(baseline, baseline with { HelperConnected = false });
        Assert.NotEqual(baseline, baseline with { WeekSeconds = 1 });
        Assert.NotEqual(baseline, baseline with { RemainingSeconds = 1 });
    }

    [Fact]
    public void DifferentStatesGiveDifferentLines()
    {
        var baseline = StateFixtures.State(addable: [StateFixtures.Named("A")]).Serialize();

        Assert.Equal(baseline, StateFixtures.State(addable: [StateFixtures.Named("A")]).Serialize());
        Assert.NotEqual(baseline, StateFixtures.State(addable: [StateFixtures.Named("B")]).Serialize());
        Assert.NotEqual(baseline, StateFixtures.State(addable: [StateFixtures.Named("A")], nightSeconds: 2821).Serialize());
        Assert.NotEqual(baseline, StateFixtures.State(addable: [StateFixtures.Named("A")], counting: null).Serialize());
        Assert.NotEqual(baseline, StateFixtures.State(GateSettings.Defaults with { CooldownHours = 24 }, addable: [StateFixtures.Named("A")]).Serialize());
        Assert.NotEqual(baseline, StateFixtures.State(GateSettings.Defaults with { Pending = [WeeknightEntry()] }, addable: [StateFixtures.Named("A")]).Serialize());
    }

    [Fact]
    public void BadStateIsNull()
    {
        var good = StateFixtures.State(StateFixtures.WithPending(WeeknightEntry())).Serialize();

        Assert.NotNull(PipeMessage.Parse(good));
        Assert.Null(PipeMessage.Parse("""{"type":"state","remainingSeconds":1,"tonightLimitMinutes":90,"tonightWeekend":false,"nightSeconds":0,"weekSeconds":0,"counting":null,"helperConnected":false,"addable":[]}"""));
        Assert.Null(PipeMessage.Parse(good.Replace(PendingId.ToString(), "not-a-guid", StringComparison.Ordinal)));
        Assert.Null(PipeMessage.Parse(good.Replace("\"Friday\"", "6", StringComparison.Ordinal)));
        Assert.Null(PipeMessage.Parse(good.Replace("\"Friday\"", "\"Someday\"", StringComparison.Ordinal)));
        Assert.Null(PipeMessage.Parse("{\"type\":\"state\""));
        Assert.Null(PipeMessage.Parse("""{"type":"state"}"""));
    }

    [Theory]
    [InlineData("\"nightSeconds\":2820", "\"nightSeconds\":\"2820\"")]
    [InlineData("\"nightSeconds\":2820", "\"nightSeconds\":null")]
    [InlineData("\"tonightWeekend\":false", "\"tonightWeekend\":0")]
    [InlineData("\"helperConnected\":true", "\"helperConnected\":\"true\"")]
    [InlineData("\"counting\":\"Cyberpunk 2077\"", "\"counting\":5")]
    [InlineData("\"tonightLimitMinutes\":90", "\"tonightLimitMinutes\":90.5")]
    [InlineData("\"tonightLimitMinutes\":90", "\"tonightLimitMinutes\":\"90\"")]
    [InlineData("\"remainingSeconds\":2580", "\"remainingSeconds\":\"x\"")]
    [InlineData("\"weeknightMinutes\":90", "\"weeknightMinutes\":9.5")]
    [InlineData("\"enabled\":true", "\"enabled\":1")]
    [InlineData("\"allowUninstall\":false", "\"allowUninstall\":\"no\"")]
    [InlineData("\"addable\":[]", "\"addable\":{}")]
    [InlineData("\"notCounted\":[{", "\"notCounted\":[5,{")]
    [InlineData("\"pending\":[]", "\"pending\":{}")]
    [InlineData("\"pending\":[]", "\"pending\":[5]")]
    public void WrongKindOfMemberIsNull(string original, string broken)
    {
        var good = StateFixtures.State(addable: []).Serialize();
        Assert.Contains(original, good, StringComparison.Ordinal);

        Assert.Null(PipeMessage.Parse(good.Replace(original, broken, StringComparison.Ordinal)));
    }

    [Fact]
    public void WrongKindInsideAnEntryIsNull()
    {
        var good = StateFixtures.State(StateFixtures.WithPending(WeeknightEntry()), addable: [StateFixtures.Named("Viewfinder")]).Serialize();

        foreach (var (original, broken) in new[]
        {
            ("\"requestedUtc\":\"2026-10-20T21:14:00Z\"", "\"requestedUtc\":\"yesterday\""),
            ("\"requestedUtc\":\"2026-10-20T21:14:00Z\"", "\"requestedUtc\":5"),
            ("\"remainingWaitSeconds\":164880", "\"remainingWaitSeconds\":\"164880\""),
            ("\"number\":120", "\"number\":\"120\""),
            ("\"field\":\"weeknightMinutes\"", "\"field\":3"),
            ("\"displayName\":\"Viewfinder\"", "\"displayName\":3"),
            ("{\"folder\":\"Viewfinder\"", "{\"directory\":\"Viewfinder\""),
        })
        {
            Assert.Contains(original, good, StringComparison.Ordinal);
            Assert.Null(PipeMessage.Parse(good.Replace(original, broken, StringComparison.Ordinal)));
        }
    }

    [Fact]
    public void NonAsciiEncoding()
    {
        var state = StateFixtures.State(counting: "Café Ω 日本", addable: [new NamedFolder("Jeu é", "日本語のゲーム")]);

        var line = state.ToLine();
        var text = Encoding.UTF8.GetString(line);

        Assert.True(Contains(line, Encoding.UTF8.GetBytes("Café Ω 日本")));
        Assert.True(Contains(line, Encoding.UTF8.GetBytes("日本語のゲーム")));
        Assert.True(Contains(line, [0xC3, 0xA9]));
        Assert.True(Contains(line, [0xE6, 0x97, 0xA5]));
        Assert.DoesNotContain("\\u", text, StringComparison.Ordinal);
        var parsed = Assert.IsType<StateMessage>(PipeMessage.Parse(text.TrimEnd('\n')));
        Assert.Equal("Café Ω 日本", parsed.Counting);
        Assert.Equal("日本語のゲーム", Assert.Single(parsed.Addable).DisplayName);
        Assert.Equal("Jeu é", parsed.Addable[0].Folder);

        var emoji = StateFixtures.State(counting: "\U0001F600");
        var emojiText = emoji.Serialize();
        Assert.Contains("\\uD83D\\uDE00", emojiText, StringComparison.Ordinal);
        Assert.Equal("\U0001F600", Assert.IsType<StateMessage>(PipeMessage.Parse(emojiText)).Counting);

        Assert.Contains("\\u00E9", new HelloMessage("é").Serialize(), StringComparison.Ordinal);
        Assert.Contains("\\u0001", StateFixtures.State(counting: "\u0001").Serialize(), StringComparison.Ordinal);
    }

    [Fact]
    public void EntryBytesIsTheSerializedSizeOfTheEntry()
    {
        var plain = new NamedFolder("Viewfinder", "Viewfinder");
        var wide = new NamedFolder("日", "\u0001");

        Assert.Equal(Encoding.UTF8.GetByteCount("{\"folder\":\"Viewfinder\",\"displayName\":\"Viewfinder\"}"), StateMessage.EntryBytes(plain));
        Assert.Equal(Encoding.UTF8.GetByteCount("{\"folder\":\"日\",\"displayName\":\"\\u0001\"}"), StateMessage.EntryBytes(wide));
    }

    [Fact]
    public void LargestStateFitsByteCap()
    {
        var control = new string('\u0001', 128);
        var apps = Enumerable.Range(0, 600).Select(index => new InstalledApp(Wide(index), control)).ToList();
        var settings = GateSettings.Defaults with
        {
            NotCounted = [.. Enumerable.Range(0, 50).Select(index => Wide(1000 + index))],
            Pending = [.. Enumerable.Range(0, 50).Select(index => new PendingChange(
                new Guid(index + 1, 0, 0, new byte[8]), ChangeFields.NotCountedAdd, null, null, Wide(2000 + index), null, StateFixtures.Now, 172800))],
        };
        var snapshot = new GateSnapshot([], apps)
        {
            Tally = Tally.Empty with { Extension = new NightExtension(new DateOnly(2026, 10, 20), 20) },
            RemainingSeconds = 100,
            TonightLimitMinutes = 110,
            Settings = settings,
        };

        var state = StatusState.From(snapshot, new DateTime(2026, 10, 20, 21, 14, 0))!;

        Assert.Equal(20, state.ExtensionMinutes);
        Assert.True(Encoding.UTF8.GetByteCount(state.Serialize()) <= StateMessage.MaxLineBytes);
        Assert.True(state.ToLine().Length <= StateMessage.MaxLineBytes + 1);
        Assert.Equal(524288, StateMessage.MaxLineBytes);
        Assert.True(state.Addable.Count is > 0 and < 512);
        var expectedOrder = apps.Select(app => app.Folder).Order(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.Equal(expectedOrder.Take(state.Addable.Count), state.Addable.Select(entry => entry.Folder));
        var oneMore = state with { Addable = [.. state.Addable, new NamedFolder(expectedOrder[state.Addable.Count], control)] };
        Assert.True(Encoding.UTF8.GetByteCount(oneMore.Serialize()) > StateMessage.MaxLineBytes);

        var shortApps = Enumerable.Range(0, 600).Select(index => new InstalledApp("g" + index, "Game " + index)).ToList();
        var small = StatusState.From(new GateSnapshot([], shortApps) { RemainingSeconds = 1, Settings = GateSettings.Defaults }, new DateTime(2026, 10, 20, 21, 14, 0))!;
        Assert.Equal(512, small.Addable.Count);

        var longName = StatusState.From(new GateSnapshot([new RunningGame("g", new string('x', 300), [])], []) { RemainingSeconds = 1, Settings = GateSettings.Defaults }, new DateTime(2026, 10, 20, 21, 14, 0))!;
        Assert.Equal(new string('x', 128), longName.Counting);
    }

    private static string Wide(int number) => new string((char)0x65E5, 256) + number.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    [Fact]
    public void ExtensionMinutesRoundTrips()
    {
        var state = StateFixtures.State(extensionMinutes: 20);

        var line = state.Serialize();
        var parsed = Assert.IsType<StateMessage>(PipeMessage.Parse(line));

        Assert.Contains("\"tonightWeekend\":false,\"extensionMinutes\":20,", line, StringComparison.Ordinal);
        Assert.Equal(20, parsed.ExtensionMinutes);
        Assert.Equal(state, parsed);
        Assert.Contains("\"extensionMinutes\":0,", StateFixtures.State().Serialize(), StringComparison.Ordinal);
        Assert.NotEqual(StateFixtures.State(extensionMinutes: 10), StateFixtures.State(extensionMinutes: 20));
        Assert.NotEqual(StateFixtures.State(), StateFixtures.State(extensionMinutes: 30));

        var withNull = line.Replace("\"extensionMinutes\":20", "\"extensionMinutes\":null", StringComparison.Ordinal);
        Assert.Equal(0, Assert.IsType<StateMessage>(PipeMessage.Parse(withNull)).ExtensionMinutes);
        foreach (var length in NightExtension.Lengths)
        {
            var text = line.Replace("\"extensionMinutes\":20", "\"extensionMinutes\":" + length, StringComparison.Ordinal);
            Assert.Equal(length, Assert.IsType<StateMessage>(PipeMessage.Parse(text)).ExtensionMinutes);
        }
    }

    [Theory]
    [InlineData("25")]
    [InlineData("-1")]
    [InlineData("\"20\"")]
    [InlineData("20.5")]
    [InlineData("20.0")]
    [InlineData("true")]
    [InlineData("[20]")]
    public void BadExtensionMinutesIsNull(string value)
    {
        var line = StateFixtures.State(extensionMinutes: 20).Serialize().Replace("\"extensionMinutes\":20", "\"extensionMinutes\":" + value, StringComparison.Ordinal);

        Assert.Null(PipeMessage.Parse(line));
    }
}
