using SteamGate.Core.Enforcement;
using SteamGate.Core.Steam;
using Xunit;

namespace SteamGate.Tests.Enforcement;

public sealed class PlannerTests
{
    private static readonly DateTime LongAgo = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ServiceStart = new(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan T = TimeSpan.FromMinutes(100);

    private static RunningGame Game(string folder, DateTime? startUtc = null, params int[] pids)
    {
        var ids = pids.Length > 0 ? pids : [10];
        return new RunningGame(folder, folder, [.. ids.Select(id => new ProcessSample(id, startUtc ?? ServiceStart.AddHours(1), @"C:\" + folder + ".exe"))]);
    }

    // Starts from a state that has passed the restart check, so only the rule under test applies.
    private static PlannerState Fresh()
    {
        var (state, _) = EnforcementPlanner.Step(PlannerState.Initial(ServiceStart), 100000, [], enabled: true, T);
        return state;
    }

    private static (PlannerState, IReadOnlyList<EnforcementAction>) Run(PlannerState state, double remaining, IReadOnlyList<RunningGame> games, TimeSpan at, bool enabled = true) =>
        EnforcementPlanner.Step(state, remaining, games, enabled, at);

    [Fact]
    public void WarnsAt30_20_10_5_1Once()
    {
        var games = new[] { Game("Viewfinder") };
        var state = Fresh();
        var seen = new List<IReadOnlyList<EnforcementAction>>();
        foreach (var remaining in new double[] { 1900, 1800, 1799, 1200, 700, 600, 599, 300, 60, 30 })
        {
            IReadOnlyList<EnforcementAction> actions;
            (state, actions) = Run(state, remaining, games, T);
            seen.Add(actions);
        }

        Assert.Empty(seen[0]);
        Assert.Equal([new Warn(30)], seen[1]);
        Assert.Empty(seen[2]);
        Assert.Equal([new Warn(20)], seen[3]);
        Assert.Empty(seen[4]);
        Assert.Equal([new Warn(10)], seen[5]);
        Assert.Empty(seen[6]);
        Assert.Equal([new Warn(5)], seen[7]);
        Assert.Equal([new Warn(1)], seen[8]);
        Assert.Empty(seen[9]);
    }

    [Fact]
    public void LateStartGetsOneWarning()
    {
        var (_, actions) = Run(Fresh(), 240, [Game("Viewfinder")], T);

        Assert.Equal([new Warn(4)], actions);
    }

    [Fact]
    public void LateStartSaysTimeLeft()
    {
        var (_, actions) = Run(Fresh(), 1201, [Game("Viewfinder")], T);

        Assert.Equal([new Warn(21)], actions);
    }

    [Fact]
    public void LateStartAtTheEdgeOfAThresholdNamesThatThreshold()
    {
        var (_, at300) = Run(Fresh(), 300, [Game("Viewfinder")], T);
        var (_, at61) = Run(Fresh(), 61, [Game("Viewfinder")], T);
        var (_, atHalf) = Run(Fresh(), 0.5, [Game("Viewfinder")], T);

        Assert.Equal([new Warn(5)], at300);
        Assert.Equal([new Warn(2)], at61);
        Assert.Equal([new Warn(1)], atHalf);
    }

    [Fact]
    public void ReplayOnlyWhileUndelivered()
    {
        var games = new[] { Game("Viewfinder") };
        var (issued, _) = Run(Fresh(), 600, games, T);
        var marked = EnforcementPlanner.MarkUndelivered(issued);

        Assert.Equal(new Warn(22), EnforcementPlanner.ReplayWarn(marked, 1300, gameOpen: true));
        Assert.Null(EnforcementPlanner.ReplayWarn(marked, 1300, gameOpen: false));
        Assert.Null(EnforcementPlanner.ReplayWarn(marked, 0, gameOpen: true));
        Assert.Null(EnforcementPlanner.ReplayWarn(issued, 1300, gameOpen: true));
        Assert.Null(EnforcementPlanner.ReplayWarn(EnforcementPlanner.MarkDelivered(marked), 1300, gameOpen: true));

        var (rearmed, _) = Run(marked, 7200, games, T);
        Assert.Null(EnforcementPlanner.ReplayWarn(rearmed, 1300, gameOpen: true));
    }

    [Fact]
    public void TheUndeliveredMarkSurvivesASameThresholdStep()
    {
        var games = new[] { Game("Viewfinder") };
        var (issued, _) = Run(Fresh(), 600, games, T);
        var marked = EnforcementPlanner.MarkUndelivered(issued);

        var (sameThreshold, _) = Run(marked, 590, games, T);
        var (stillNone, _) = Run(EnforcementPlanner.MarkUndelivered(Fresh()), 5000, games, T);

        Assert.True(sameThreshold.WarnUndelivered);
        Assert.True(stillNone.WarnUndelivered);
    }

    [Fact]
    public void ANewWarningKeepsTheUndeliveredMark()
    {
        var games = new[] { Game("Viewfinder") };
        var (issued, _) = Run(Fresh(), 600, games, T);
        var (next, actions) = Run(EnforcementPlanner.MarkUndelivered(issued), 300, games, T);

        Assert.Equal([new Warn(5)], actions);
        Assert.True(next.WarnUndelivered);
    }

    [Fact]
    public void AGameWithNoProcessesIsNotTakenForOneOpenBeforeTheRestart()
    {
        var empty = new RunningGame("Viewfinder", "Viewfinder", []);

        var (_, actions) = Run(PlannerState.Initial(ServiceStart), 0, [empty], T);

        Assert.Equal([new PoliteClose(empty)], actions);
    }

    [Fact]
    public void ADisabledStepClearsTheUndeliveredMark()
    {
        var games = new[] { Game("Viewfinder") };
        var (issued, _) = Run(Fresh(), 600, games, T);
        var marked = EnforcementPlanner.MarkUndelivered(issued);

        var (afterDisabled, actions) = Run(marked, 590, games, T, enabled: false);
        var (afterDisabledAtZero, _) = Run(marked, 0, games, T, enabled: false);

        Assert.Empty(actions);
        Assert.False(afterDisabled.WarnUndelivered);
        Assert.False(afterDisabledAtZero.WarnUndelivered);
        Assert.Null(EnforcementPlanner.ReplayWarn(afterDisabled, 590, gameOpen: true));
    }

    [Fact]
    public void MarksAreIndependentOfEachOther()
    {
        var state = Fresh();

        Assert.True(EnforcementPlanner.MarkUndelivered(state).WarnUndelivered);
        Assert.False(EnforcementPlanner.MarkDelivered(EnforcementPlanner.MarkUndelivered(state)).WarnUndelivered);
        Assert.False(state.WarnUndelivered);
    }

    [Fact]
    public void NoGameOrUnknownNoWarning()
    {
        var (_, noGame) = Run(Fresh(), 300, [], T);
        var (_, unknown) = Run(Fresh(), double.NaN, [Game("Viewfinder")], T);

        Assert.Empty(noGame);
        Assert.Empty(unknown);
    }

    [Fact]
    public void ABlankStepKeepsTheWarningForWhenAGameStarts()
    {
        var (state, none) = Run(Fresh(), 240, [], T);
        var (_, later) = Run(state, 235, [Game("Viewfinder")], T + TimeSpan.FromSeconds(5));

        Assert.Empty(none);
        Assert.Equal([new Warn(4)], later);
    }

    [Fact]
    public void PoliteThenForceAndRetry()
    {
        var game = Game("Viewfinder", null, 20, 21);
        var state = Fresh();
        var results = new List<IReadOnlyList<EnforcementAction>>();
        foreach (var offset in new[] { 0, 30, 59, 60, 65 })
        {
            IReadOnlyList<EnforcementAction> actions;
            (state, actions) = Run(state, 0, [game], T + TimeSpan.FromSeconds(offset));
            results.Add(actions);
        }

        Assert.Equal([new PoliteClose(game)], results[0]);
        Assert.Empty(results[1]);
        Assert.Empty(results[2]);
        Assert.Equal([new ForceClose(game)], results[3]);
        Assert.Equal([new ForceClose(game)], results[4]);
    }

    [Fact]
    public void PendingCloseDroppedWhenTimeReturns()
    {
        var game = Game("Viewfinder", null, 20, 21);
        var (state, first) = Run(Fresh(), 0, [game], T);
        (state, _) = Run(state, 600, [game], T + TimeSpan.FromSeconds(30));
        var (_, later) = Run(state, 600, [game], T + TimeSpan.FromSeconds(60));

        Assert.Equal([new PoliteClose(game)], first);
        Assert.DoesNotContain(later, action => action is ForceClose);
    }

    [Fact]
    public void ALimitReachedAgainStartsAFreshPoliteClose()
    {
        var game = Game("Viewfinder");
        var (state, _) = Run(Fresh(), 0, [game], T);
        (state, _) = Run(state, 600, [game], T + TimeSpan.FromSeconds(30));
        var (_, again) = Run(state, 0, [game], T + TimeSpan.FromSeconds(61));

        Assert.Equal([new PoliteClose(game)], again);
    }

    [Fact]
    public void RestartDoesNotResetGrace()
    {
        var early = Game("Viewfinder", LongAgo);
        var late = Game("Viewfinder", ServiceStart.AddSeconds(5));

        var (_, forced) = Run(PlannerState.Initial(ServiceStart), 0, [early], T);
        var (_, polite) = Run(PlannerState.Initial(ServiceStart), 0, [late], T);
        var (afterNaN, nothing) = Run(PlannerState.Initial(ServiceStart), double.NaN, [early], T);
        var (_, forcedAfterNaN) = Run(afterNaN, 0, [early], T);

        Assert.Equal([new ForceClose(early)], forced);
        Assert.Equal([new PoliteClose(late)], polite);
        Assert.Empty(nothing);
        Assert.Equal([new ForceClose(early)], forcedAfterNaN);
    }

    [Fact]
    public void RestartCheckNeedsEveryProcessOlderThanTheService()
    {
        var mixed = new RunningGame(
            "Viewfinder",
            "Viewfinder",
            [
                new ProcessSample(1, LongAgo, @"C:\a.exe"),
                new ProcessSample(2, ServiceStart.AddSeconds(1), @"C:\b.exe"),
            ]);
        var exactlyAtStart = Game("Other", ServiceStart);

        var (_, actions) = Run(PlannerState.Initial(ServiceStart), 0, [mixed, exactlyAtStart], T);

        Assert.Equal([new PoliteClose(mixed), new PoliteClose(exactlyAtStart)], actions);
    }

    [Fact]
    public void RestartCheckIsUsedUpByTheFirstKnownStep()
    {
        var early = Game("Viewfinder", LongAgo);
        var (state, _) = Run(PlannerState.Initial(ServiceStart), 600, [], T);

        var (_, actions) = Run(state, 0, [early], T + TimeSpan.FromSeconds(5));

        Assert.Equal([new PoliteClose(early)], actions);
    }

    [Fact]
    public void RestartForceKeepsForcingEveryStep()
    {
        var early = Game("Viewfinder", LongAgo);
        var (state, first) = Run(PlannerState.Initial(ServiceStart), 0, [early], T);
        var (_, second) = Run(state, 0, [early], T + TimeSpan.FromSeconds(5));

        Assert.Equal([new ForceClose(early)], first);
        Assert.Equal([new ForceClose(early)], second);
    }

    [Fact]
    public void GameStartedAfterLimitIsClosed()
    {
        var viewfinder = Game("Viewfinder");
        var overwatch = Game("Overwatch", null, 30);
        var state = Fresh();
        (state, _) = Run(state, 0, [viewfinder], T);
        (state, _) = Run(state, 0, [], T + TimeSpan.FromSeconds(10));

        var u = T + TimeSpan.FromSeconds(100);
        var (afterU, atU) = Run(state, 0, [overwatch], u);
        var (afterU59, at59) = Run(afterU, 0, [overwatch], u + TimeSpan.FromSeconds(59));
        var (_, at60) = Run(afterU59, 0, [overwatch], u + TimeSpan.FromSeconds(60));

        Assert.Equal([new PoliteClose(overwatch)], atU);
        Assert.Empty(at59);
        Assert.Equal([new ForceClose(overwatch)], at60);

        // The second half: it closes at U+20 s and reopens at U+40 s, and is force closed at once.
        var (reopenState, _) = Run(state, 0, [overwatch], u);
        (reopenState, _) = Run(reopenState, 0, [], u + TimeSpan.FromSeconds(20));
        var (_, atReopen) = Run(reopenState, 0, [overwatch], u + TimeSpan.FromSeconds(40));
        Assert.Equal([new ForceClose(overwatch)], atReopen);
    }

    [Fact]
    public void ReopeningAfterCloseIsForcedAtOnce()
    {
        var overwatch = Game("Overwatch", null, 30);
        var (state, atU) = Run(Fresh(), 0, [overwatch], T);
        (state, _) = Run(state, 0, [], T + TimeSpan.FromSeconds(20));
        var (_, atReopen) = Run(state, 0, [overwatch], T + TimeSpan.FromSeconds(40));

        Assert.Equal([new PoliteClose(overwatch)], atU);
        Assert.Equal([new ForceClose(overwatch)], atReopen);
    }

    [Fact]
    public void FoldersAreMatchedIgnoringCase()
    {
        var lower = Game("viewfinder");
        var upper = Game("VIEWFINDER");
        var (state, _) = Run(Fresh(), 0, [lower], T);
        (state, _) = Run(state, 0, [], T + TimeSpan.FromSeconds(1));

        var (_, actions) = Run(state, 0, [upper], T + TimeSpan.FromSeconds(2));

        Assert.Equal([new ForceClose(upper)], actions);
    }

    [Fact]
    public void TwoGamesAreTrackedSeparately()
    {
        var a = Game("A");
        var b = Game("B", null, 11);
        var (state, first) = Run(Fresh(), 0, [a], T);
        var (state2, second) = Run(state, 0, [a, b], T + TimeSpan.FromSeconds(30));
        var (_, third) = Run(state2, 0, [a, b], T + TimeSpan.FromSeconds(60));

        Assert.Equal([new PoliteClose(a)], first);
        Assert.Equal([new PoliteClose(b)], second);
        Assert.Equal([new ForceClose(a)], third);
    }

    [Fact]
    public void DisabledDoesNothing()
    {
        var game = Game("Viewfinder");

        var (state, closing) = Run(Fresh(), 0, [game], T, enabled: false);
        var (_, warning) = Run(Fresh(), 240, [game], T, enabled: false);
        var (_, afterEnable) = Run(state, 0, [game], T + TimeSpan.FromSeconds(5));

        Assert.Empty(closing);
        Assert.Empty(warning);
        Assert.Equal([new PoliteClose(game)], afterEnable);
    }

    [Fact]
    public void DisablingDropsCloseState()
    {
        var game = Game("Viewfinder");
        var (state, _) = Run(Fresh(), 0, [game], T);
        (state, _) = Run(state, 0, [game], T + TimeSpan.FromSeconds(10), enabled: false);

        var (_, actions) = Run(state, 0, [game], T + TimeSpan.FromSeconds(70));

        Assert.Equal([new PoliteClose(game)], actions);
    }

    [Fact]
    public void WarningsResetWhenTimeComesBack()
    {
        var games = new[] { Game("Viewfinder") };
        var state = Fresh();
        var warned = new List<IReadOnlyList<EnforcementAction>>();
        foreach (var remaining in new double[] { 600, 7200, 1800, 1200, 600 })
        {
            IReadOnlyList<EnforcementAction> actions;
            (state, actions) = Run(state, remaining, games, T);
            warned.Add(actions);
        }

        Assert.Equal([new Warn(10)], warned[0]);
        Assert.Empty(warned[1]);
        Assert.Equal([new Warn(30)], warned[2]);
        Assert.Equal([new Warn(20)], warned[3]);
        Assert.Equal([new Warn(10)], warned[4]);
    }

    [Fact]
    public void ARaisedLimitRearmsOnlyTheThresholdsBelowTheNewTime()
    {
        var games = new[] { Game("Viewfinder") };
        var (state, _) = Run(Fresh(), 60, games, T);
        (state, _) = Run(state, 700, games, T);
        var (_, at600) = Run(state, 600, games, T);
        var (_, at1200) = Run(state, 1200, games, T);

        Assert.Equal([new Warn(10)], at600);
        Assert.Empty(at1200);
    }

    [Fact]
    public void ZeroRemainingNeverWarns()
    {
        var (_, actions) = Run(Fresh(), 0, [Game("Viewfinder")], T);

        Assert.DoesNotContain(actions, action => action is Warn);
    }

    [Fact]
    public void ThresholdsAndGraceAreTheApprovedValues()
    {
        Assert.Equal([1800, 1200, 600, 300, 60], EnforcementPlanner.WarnThresholdSeconds);
        Assert.Equal(TimeSpan.FromSeconds(60), EnforcementPlanner.PoliteGrace);
    }

    [Fact]
    public void ExtensionRearmsTheWarnings()
    {
        var games = new[] { Game("Viewfinder") };

        // An extension of 10 minutes between the second and third reading: 1190 s left, then 1785.
        var state = Fresh();
        var seen = new List<IReadOnlyList<EnforcementAction>>();
        foreach (var remaining in new double[] { 1790, 1190, 1785, 1190 })
        {
            IReadOnlyList<EnforcementAction> actions;
            (state, actions) = Run(state, remaining, games, T);
            seen.Add(actions);
        }

        Assert.Equal([new Warn(30)], seen[0]);
        Assert.Equal([new Warn(20)], seen[1]);
        Assert.Empty(seen[2]);
        Assert.Equal([new Warn(20)], seen[3]);

        // An extension of 20 minutes: 1190 s left, then 2300, then back through 1790 and 1190.
        state = Fresh();
        seen.Clear();
        foreach (var remaining in new double[] { 1190, 2300, 1790, 1190 })
        {
            IReadOnlyList<EnforcementAction> actions;
            (state, actions) = Run(state, remaining, games, T);
            seen.Add(actions);
        }

        Assert.Equal([new Warn(20)], seen[0]);
        Assert.Empty(seen[1]);
        Assert.Equal([new Warn(30)], seen[2]);
        Assert.Equal([new Warn(20)], seen[3]);

        // A polite close in progress is forgotten once time is left: the next limit starts a fresh polite close, not a force close.
        var overwatch = Game("Overwatch", null, 30);
        var (closing, atLimit) = Run(Fresh(), 0, [overwatch], T);
        var (extended, atThirty) = Run(closing, 600, [overwatch], T + TimeSpan.FromSeconds(30));
        var (_, atAgain) = Run(extended, 0, [overwatch], T + TimeSpan.FromSeconds(120));

        Assert.Equal([new PoliteClose(overwatch)], atLimit);
        Assert.DoesNotContain(atThirty, action => action is ForceClose);
        Assert.Equal([new PoliteClose(overwatch)], atAgain);
    }
}
