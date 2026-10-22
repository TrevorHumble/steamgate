using SteamGate.Core.Steam;

namespace SteamGate.Core.Accounting;

/// <summary>Counts the time games are open. A pure function of its inputs.</summary>
public static class Accountant
{
    /// <summary>The most one tick counts, so a sleep or hibernate gap adds at most this much.</summary>
    public static readonly TimeSpan MaxTickStep = TimeSpan.FromSeconds(15);

    /// <summary>The most downtime counted on the first tick after the service starts.</summary>
    public static readonly TimeSpan MaxRestartGap = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Returns the tally after one tick. The night and week roll over first. When any game is open, the step
    /// (never more than maxStep, never below 0) is added once to the night and the week, however many games are open,
    /// and LastCountedUtc becomes utcNow. When no game is open nothing is added and LastCountedUtc becomes null.
    /// </summary>
    public static Tally Tick(
        Tally current,
        DateTime localNow,
        DateTime utcNow,
        TimeSpan monotonicStep,
        TimeSpan maxStep,
        IReadOnlyCollection<RunningGame> openGames)
    {
        var rolled = NightClock.RollOver(current, localNow);
        if (openGames.Count == 0)
        {
            return rolled with { LastCountedUtc = null };
        }

        var seconds = Math.Max(0, Math.Min(monotonicStep.TotalSeconds, maxStep.TotalSeconds));
        return rolled with
        {
            NightSeconds = rolled.NightSeconds + seconds,
            WeekSeconds = rolled.WeekSeconds + seconds,
            LastCountedUtc = utcNow,
        };
    }

    /// <summary>
    /// The step for the first tick after the service starts: the time since the last count, from 0 to MaxRestartGap,
    /// when a game is open now and a game was open at the last count; otherwise 0.
    /// </summary>
    public static TimeSpan RestartStep(Tally loaded, DateTime utcNow, bool anyGameOpen)
    {
        if (!anyGameOpen || loaded.LastCountedUtc is not { } last)
        {
            return TimeSpan.Zero;
        }

        var seconds = Math.Clamp((utcNow - last).TotalSeconds, 0, MaxRestartGap.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
