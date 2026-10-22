using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class SkipTrackerTests
{
    [Fact]
    public void A_PathSkippedAgainOnTheNextRefresh_IsNotNew()
    {
        var tracker = new SkipTracker();

        var first = tracker.Add(@"C:\steamapps\appmanifest_1.acf");
        var repeatInSameRefresh = tracker.Add(@"c:\STEAMAPPS\appmanifest_1.acf");
        tracker.EndRefresh();
        var next = tracker.Add(@"C:\steamapps\appmanifest_1.acf");
        var other = tracker.Add(@"C:\steamapps\appmanifest_2.acf");

        Assert.Equal([true, false, false, true], new[] { first, repeatInSameRefresh, next, other });
    }

    [Fact]
    public void A_PathThatStoppedBeingSkipped_IsNewAgainWhenItReturns()
    {
        var tracker = new SkipTracker();
        _ = tracker.Add(@"C:\steamapps\appmanifest_1.acf");
        tracker.EndRefresh();
        tracker.EndRefresh();

        Assert.True(tracker.Add(@"C:\steamapps\appmanifest_1.acf"));
    }
}
