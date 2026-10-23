using System.Text.RegularExpressions;
using SteamGate.Core;
using SteamGate.Core.Rules;
using Xunit;

namespace SteamGate.Tests;

public sealed class SettingsStoreRuleTests
{
    private const string Entry = """{"id":"0f8fad5b-d9cb-469f-a165-70867728950e","field":"enabled","flag":false,"requestedUtc":"2026-10-20T21:00:00Z","remainingWaitSeconds":REMAIN}""";

    private static PendingChange Filler(int n) =>
        new(Guid.NewGuid(), ChangeFields.WeeklyCapMinutes, 700 + n, null, null, null, DateTime.UtcNow, 5);

    [Fact]
    public void SaveRefusesShapesThatBreakTheRules()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Folder);
        store.Load();
        var before = File.ReadAllText(temp.PathOf("settings.json"));
        var fifty = Enumerable.Range(0, 50).Select(Filler).ToArray();

        Assert.False(store.Save(GateSettings.Defaults with { Pending = [.. fifty, Filler(50)] }));
        Assert.False(store.Save(GateSettings.Defaults with { NotCounted = ["Blender", "a/b"] }));
        Assert.False(store.Save(GateSettings.Defaults with { NotCounted = [string.Empty] }));
        Assert.False(store.Save(GateSettings.Defaults with { NotCounted = [new string('x', 261)] }));
        Assert.False(store.Save(GateSettings.Defaults with { WeekendNights = [DayOfWeek.Friday, DayOfWeek.Friday] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = double.PositiveInfinity }] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = double.NaN }] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = 604801 }] }));
        Assert.False(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = -1 }] }));
        Assert.Equal(before, File.ReadAllText(temp.PathOf("settings.json")));

        Assert.True(store.Save(GateSettings.Defaults with { Pending = fifty }));
        Assert.True(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = 604800 }] }));
        Assert.True(store.Save(GateSettings.Defaults with { Pending = [Filler(0) with { RemainingWaitSeconds = 0 }] }));
    }

    [Theory]
    [InlineData("""{"notCounted":["Blender","a\\b"]}""")]
    [InlineData("""{"notCounted":[""]}""")]
    [InlineData("""{"weekendNights":["Friday","Friday"]}""")]
    public void LoadFallsBackWhenTheFileBreaksARule(string content)
    {
        AssertCorrupt(content);
    }

    [Theory]
    [InlineData("604801")]
    [InlineData("-5")]
    public void LoadFallsBackForABadWait(string remaining)
    {
        AssertCorrupt("""{"pending":[""" + Entry.Replace("REMAIN", remaining, StringComparison.Ordinal) + "]}");
    }

    [Fact]
    public void LoadKeepsAWaitAtTheEdges()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), """{"pending":[""" + Entry.Replace("REMAIN", "604800", StringComparison.Ordinal) + "]}");

        var result = new SettingsStore(temp.Folder).Load();

        Assert.False(result.Corrupt);
        Assert.Equal(604800, Assert.Single(result.Value.Pending).RemainingWaitSeconds);
    }

    [Fact]
    public void LoadFallsBackForMoreThanFiftyPendingEntries()
    {
        var one = Entry.Replace("REMAIN", "10", StringComparison.Ordinal);

        AssertCorrupt("""{"pending":[""" + string.Join(",", Enumerable.Repeat(one, 51)) + "]}");
    }

    private static void AssertCorrupt(string content)
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("settings.json"), content);

        var result = new SettingsStore(temp.Folder).Load();

        Assert.True(result.Corrupt);
        Assert.Equal(90, result.Value.WeeknightMinutes);
        Assert.Empty(result.Value.Pending);
        var kept = Assert.Single(Directory.GetFiles(temp.Folder, "settings.corrupt-*"));
        Assert.Matches(new Regex(@"^settings\.corrupt-\d{8}T\d{9}Z\.json$"), Path.GetFileName(kept));
        Assert.Equal(content, File.ReadAllText(kept));
    }
}
