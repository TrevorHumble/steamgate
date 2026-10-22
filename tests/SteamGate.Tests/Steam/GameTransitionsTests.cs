using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Steam;

public sealed class GameTransitionsTests
{
    [Fact]
    public void Diff_StartedAndStopped()
    {
        var (started, stopped) = GameTransitions.Diff(["A", "B"], ["B", "C"]);

        Assert.Equal(["C"], started);
        Assert.Equal(["A"], stopped);
    }

    [Fact]
    public void Diff_IgnoresCase()
    {
        var (started, stopped) = GameTransitions.Diff(["Cyberpunk 2077"], ["cyberpunk 2077"]);

        Assert.Empty(started);
        Assert.Empty(stopped);
    }

    [Fact]
    public void Diff_SortedIgnoringCase_NoRepeats()
    {
        var (started, stopped) = GameTransitions.Diff(["z", "Y", "y"], ["b", "A", "a", "B"]);

        Assert.Equal(["A", "b"], started);
        Assert.Equal(["Y", "z"], stopped);
    }

    [Fact]
    public void Diff_FromNothing_StartsEverything()
    {
        var (started, stopped) = GameTransitions.Diff([], ["G"]);

        Assert.Equal(["G"], started);
        Assert.Empty(stopped);
    }
}
