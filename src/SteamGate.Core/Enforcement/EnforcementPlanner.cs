using SteamGate.Core.Steam;

namespace SteamGate.Core.Enforcement;

/// <summary>
/// Decides what to warn about and what to close. Pure: the worker feeds it the time left and the open games
/// once per tick and carries out the actions it returns.
/// </summary>
public static class EnforcementPlanner
{
    /// <summary>Warning times in seconds, largest first: 30, 20, 10, 5, and 1 minutes.</summary>
    public static readonly IReadOnlyList<int> WarnThresholdSeconds = [1800, 1200, 600, 300, 60];

    /// <summary>How long a game has to close after the polite close before it is force closed.</summary>
    public static readonly TimeSpan PoliteGrace = TimeSpan.FromSeconds(60);

    /// <param name="state">What the last step returned.</param>
    /// <param name="remainingSeconds">Seconds left; NaN means not known yet, and gives no actions.</param>
    /// <param name="openGames">The games running now.</param>
    /// <param name="enabled">False when SteamGate is turned off; nothing is done then.</param>
    /// <param name="monotonicNow">A clock that never goes back, such as a Stopwatch reading.</param>
    public static (PlannerState Next, IReadOnlyList<EnforcementAction> Actions) Step(
        PlannerState state,
        double remainingSeconds,
        IReadOnlyList<RunningGame> openGames,
        bool enabled,
        TimeSpan monotonicNow)
    {
        var (next, actions) = StepCore(state, remainingSeconds, openGames, enabled, monotonicNow);

        // Turned off means nothing is done and nothing is owed, so a held warning is dropped too.
        return (enabled ? next : next with { WarnUndelivered = false }, actions);
    }

    private static (PlannerState Next, IReadOnlyList<EnforcementAction> Actions) StepCore(
        PlannerState state,
        double remainingSeconds,
        IReadOnlyList<RunningGame> openGames,
        bool enabled,
        TimeSpan monotonicNow)
    {
        if (double.IsNaN(remainingSeconds))
        {
            return (state, []);
        }

        var restartCheck = state.RestartCheckPending;
        var next = state with { RestartCheckPending = false };
        var actions = new List<EnforcementAction>();

        if (remainingSeconds > 0)
        {
            next = next with { Closing = state.Closing.Clear() };
            next = PlanWarnings(next, remainingSeconds, openGames.Count > 0 && enabled, actions);
            return (next, actions);
        }

        if (!enabled)
        {
            return (next with { Closing = state.Closing.Clear() }, actions);
        }

        return (PlanClosing(next, restartCheck, openGames, monotonicNow, actions), actions);
    }

    /// <summary>The warning to show again when a helper has just connected, or null when none is owed.</summary>
    public static Warn? ReplayWarn(PlannerState state, double remainingSeconds, bool gameOpen) =>
        state.WarnUndelivered && gameOpen && remainingSeconds > 0 ? new Warn(WarnMinutes.FromRemaining(remainingSeconds)) : null;

    /// <summary>Records that a warning found no helper to show it.</summary>
    public static PlannerState MarkUndelivered(PlannerState state) => state with { WarnUndelivered = true };

    /// <summary>Records that a warning reached a helper.</summary>
    public static PlannerState MarkDelivered(PlannerState state) => state with { WarnUndelivered = false };

    // The smallest threshold at or above the time left is the one now reached. It going down is a new warning;
    // it going up means the larger thresholds are armed again.
    private static PlannerState PlanWarnings(PlannerState state, double remainingSeconds, bool warnable, List<EnforcementAction> actions)
    {
        int? reached = null;
        foreach (var threshold in WarnThresholdSeconds)
        {
            if (threshold >= remainingSeconds && (reached is null || threshold < reached))
            {
                reached = threshold;
            }
        }

        // No threshold reached counts as larger than every threshold.
        var reachedRank = reached ?? int.MaxValue;
        var issuedRank = state.SmallestIssuedSeconds ?? int.MaxValue;
        if (reachedRank > issuedRank)
        {
            return state with { SmallestIssuedSeconds = reached, WarnUndelivered = false };
        }

        if (reachedRank < issuedRank && warnable)
        {
            actions.Add(new Warn(WarnMinutes.FromRemaining(remainingSeconds)));
            return state with { SmallestIssuedSeconds = reached };
        }

        return state;
    }

    private static PlannerState PlanClosing(
        PlannerState state,
        bool restartCheck,
        IReadOnlyList<RunningGame> openGames,
        TimeSpan now,
        List<EnforcementAction> actions)
    {
        var closing = state.Closing;
        var openFolders = new HashSet<string>(openGames.Select(game => game.Folder), StringComparer.OrdinalIgnoreCase);

        // A game that is gone must be forced at once if it comes back.
        foreach (var (folder, entry) in state.Closing)
        {
            if (!openFolders.Contains(folder) && !entry.ForceNow)
            {
                closing = closing.SetItem(folder, entry with { ForceNow = true });
            }
        }

        foreach (var game in openGames)
        {
            if (closing.TryGetValue(game.Folder, out var entry))
            {
                if (entry.ForceNow || now - entry.PoliteAt >= PoliteGrace)
                {
                    actions.Add(new ForceClose(game));
                }
            }
            else if (restartCheck && OpenBeforeStart(game, state.ServiceStartUtc))
            {
                actions.Add(new ForceClose(game));
                closing = closing.SetItem(game.Folder, new CloseEntry(now, ForceNow: true));
            }
            else
            {
                actions.Add(new PoliteClose(game));
                closing = closing.SetItem(game.Folder, new CloseEntry(now, ForceNow: false));
            }
        }

        return state with { Closing = closing };
    }

    private static bool OpenBeforeStart(RunningGame game, DateTime serviceStartUtc) =>
        game.Processes.Count > 0 && game.Processes.All(process => process.StartUtc < serviceStartUtc);
}
