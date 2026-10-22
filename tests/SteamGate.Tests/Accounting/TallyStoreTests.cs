using System.Text.RegularExpressions;
using SteamGate.Core.Accounting;
using Xunit;

namespace SteamGate.Tests.Accounting;

public sealed class TallyStoreTests
{
    private static readonly Tally Sample = new(
        new DateOnly(2026, 10, 22),
        1234.567,
        new DateOnly(2026, 10, 19),
        20000.25,
        new DateTime(2026, 10, 22, 21, 0, 5, 123, DateTimeKind.Utc));

    [Fact]
    public void Load_Missing_WritesAndReturnsEmpty()
    {
        using var temp = new TempFolder();

        var result = new TallyStore(temp.Folder).Load();

        Assert.Equal(Tally.Empty, result.Value);
        Assert.False(result.Corrupt);
        Assert.True(File.Exists(temp.PathOf("tally.json")));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        using var temp = new TempFolder();
        var store = new TallyStore(temp.Folder);
        store.Load();

        Assert.True(store.Save(Sample));
        var loaded = new TallyStore(temp.Folder).Load();

        Assert.False(loaded.Corrupt);
        Assert.Equal(new DateOnly(2026, 10, 22), loaded.Value.Night);
        Assert.Equal(1234.567, loaded.Value.NightSeconds);
        Assert.Equal(new DateOnly(2026, 10, 19), loaded.Value.Week);
        Assert.Equal(20000.25, loaded.Value.WeekSeconds);
        Assert.Equal(Sample.LastCountedUtc, loaded.Value.LastCountedUtc);
        Assert.Equal(DateTimeKind.Utc, loaded.Value.LastCountedUtc!.Value.Kind);
    }

    [Fact]
    public void SaveThenLoad_NullLastCounted_RoundTrips()
    {
        using var temp = new TempFolder();
        var store = new TallyStore(temp.Folder);
        store.Load();

        Assert.True(store.Save(Sample with { LastCountedUtc = null }));

        Assert.Null(new TallyStore(temp.Folder).Load().Value.LastCountedUtc);
    }

    [Fact]
    public void Load_Corrupt_ResetsToZero()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("tally.json"), "not json");

        var result = new TallyStore(temp.Folder).Load();

        Assert.True(result.Corrupt);
        Assert.Equal(0, result.Value.NightSeconds);
        Assert.Equal(0, result.Value.WeekSeconds);
        var kept = Assert.Single(Directory.GetFiles(temp.Folder, "tally.corrupt-*"));
        Assert.Matches(new Regex(@"^tally\.corrupt-\d{8}T\d{9}Z\.json$"), Path.GetFileName(kept));
        Assert.Equal("not json", File.ReadAllText(kept));
        Assert.False(new TallyStore(temp.Folder).Load().Corrupt);
    }

    [Theory]
    [InlineData("{\"nightSeconds\":-1}")]
    [InlineData("{\"weekSeconds\":-0.5}")]
    [InlineData("null")]
    public void Load_InvalidContent_ResetsToZero(string content)
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.PathOf("tally.json"), content);

        var result = new TallyStore(temp.Folder).Load();

        Assert.True(result.Corrupt);
        Assert.Equal(Tally.Empty, result.Value);
    }

    [Fact]
    public void Save_InvalidCounts_ReturnsFalseAndKeepsFile()
    {
        using var temp = new TempFolder();
        var store = new TallyStore(temp.Folder);
        store.Load();
        Assert.True(store.Save(Sample));
        var before = File.ReadAllText(temp.PathOf("tally.json"));

        Assert.False(store.Save(Sample with { NightSeconds = -1 }));
        Assert.False(store.Save(Sample with { WeekSeconds = -1 }));
        Assert.False(store.Save(Sample with { NightSeconds = double.NaN }));
        Assert.False(store.Save(Sample with { WeekSeconds = double.PositiveInfinity }));

        Assert.Equal(before, File.ReadAllText(temp.PathOf("tally.json")));
    }

    [Fact]
    public void Save_ZeroCounts_Accepted()
    {
        using var temp = new TempFolder();
        var store = new TallyStore(temp.Folder);
        store.Load();

        Assert.True(store.Save(Sample with { NightSeconds = 0, WeekSeconds = 0 }));
    }
}
