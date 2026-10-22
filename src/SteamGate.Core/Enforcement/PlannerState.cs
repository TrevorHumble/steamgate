using System.Collections.Immutable;

namespace SteamGate.Core.Enforcement;

/// <summary>What the planner remembers between steps. Never changed; each step returns the next one. Only the planner writes it.</summary>
public sealed record PlannerState
{
    /// <summary>The smallest warning threshold (in seconds) already issued; every larger one counts as issued. Null means none.</summary>
    internal int? SmallestIssuedSeconds { get; init; }

    /// <summary>True when a warning was issued and no helper was there to show it.</summary>
    public bool WarnUndelivered { get; internal init; }

    /// <summary>Per game folder (ignoring case): when its polite close was issued, and whether the next step must force it.</summary>
    internal ImmutableDictionary<string, CloseEntry> Closing { get; init; } = ImmutableDictionary.Create<string, CloseEntry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The start of this service process; a game whose processes all started earlier was open before the restart.</summary>
    internal DateTime ServiceStartUtc { get; init; }

    /// <summary>True until the first step whose remaining time is known.</summary>
    internal bool RestartCheckPending { get; init; }

    public static PlannerState Initial(DateTime serviceStartUtc) => new() { ServiceStartUtc = serviceStartUtc, RestartCheckPending = true };
}

/// <summary>A game's close state. PoliteAt is the monotonic time of the polite close. ForceNow means force at once (reopened, or open before a restart).</summary>
internal sealed record CloseEntry(TimeSpan PoliteAt, bool ForceNow);
