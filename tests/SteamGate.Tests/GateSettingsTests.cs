using SteamGate.Core;
using Xunit;

namespace SteamGate.Tests;

public sealed class GateSettingsTests
{
    [Fact]
    public void ApprovedNames_MapFolderToSpecName()
    {
        var names = GateSettings.ApprovedNames;

        Assert.Equal(4, names.Count);
        Assert.Equal("Wallpaper Engine", names["wallpaper_engine"]);
        Assert.Equal("Blender", names["Blender"]);
        Assert.Equal("OBS Studio", names["OBS Studio"]);
        Assert.Equal("Steamworks Shared", names["Steamworks Shared"]);
    }

    [Fact]
    public void Defaults_AreBuilt_WithNonNullLists()
    {
        Assert.Equal([DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday], GateSettings.Defaults.WeekendNights);
        Assert.Equal(["Blender", "OBS Studio", "wallpaper_engine", "Steamworks Shared"], GateSettings.Defaults.NotCounted);
    }

    [Fact]
    public void Defaults_NotCounted_AreAllApprovedNames()
    {
        Assert.Equal(GateSettings.ApprovedNames.Count, GateSettings.Defaults.NotCounted.Count);
        Assert.All(GateSettings.Defaults.NotCounted, name => Assert.True(GateSettings.ApprovedNames.ContainsKey(name)));
    }
}
