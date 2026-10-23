using SteamGate.Core.Accounting;
using SteamGate.Core.Steam;

namespace SteamGate.Core;

/// <summary>What the worker knew at the end of its last tick. Replaced each tick, never changed; later issues add init-only properties.</summary>
public sealed record GateSnapshot(IReadOnlyList<RunningGame> Games, IReadOnlyList<InstalledApp> Installed)
{
    public static GateSnapshot Empty { get; } = new([], []);

    /// <summary>The seconds counted tonight and this week, as of the last tick.</summary>
    public Tally Tally { get; init; } = Tally.Empty;

    /// <summary>Seconds left tonight or this week, whichever is less. Null until the first tick has computed it.</summary>
    public double? RemainingSeconds { get; init; }

    /// <summary>Tonight's limit in minutes. Null until the first tick has computed it.</summary>
    public int? TonightLimitMinutes { get; init; }

    /// <summary>True when a verified helper (the overlay program in the user's session) is connected to the pipe.</summary>
    public bool HelperConnected { get; init; }

    /// <summary>The settings the worker holds, including pending changes. Null until the first tick has loaded them; readers treat null as not yet loaded.</summary>
    public GateSettings? Settings { get; init; }
}
