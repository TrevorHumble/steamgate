using SteamGate.Core.Steam;

namespace SteamGate.Core;

/// <summary>What the worker knew at the end of its last tick. Replaced each tick, never changed; later issues add fields with "with".</summary>
public sealed record GateSnapshot(IReadOnlyList<RunningGame> Games, IReadOnlyList<InstalledApp> Installed)
{
    public static GateSnapshot Empty { get; } = new([], []);
}
