using SteamGate.Core.Steam;

namespace SteamGate.Core.Enforcement;

/// <summary>Something the planner wants done. The worker carries it out.</summary>
public abstract record EnforcementAction;

/// <summary>Show a warning that this many whole minutes are left.</summary>
public sealed record Warn(int Minutes) : EnforcementAction;

/// <summary>Ask the game to close. The game is the one from the current step's open games.</summary>
public sealed record PoliteClose(RunningGame Game) : EnforcementAction;

/// <summary>End the game's processes. Repeated on every step while the game stays open.</summary>
public sealed record ForceClose(RunningGame Game) : EnforcementAction;
