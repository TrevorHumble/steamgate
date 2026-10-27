namespace SteamGate.Core.Accounting;

/// <summary>The minutes added to one night's limit, taken once that night. Saved inside tally.json.</summary>
public sealed record NightExtension(DateOnly Night, int Minutes)
{
    /// <summary>The allowed lengths in minutes: the one list that the file check, the pipe, and the Extend rules share.</summary>
    public static IReadOnlyList<int> Lengths { get; } = [10, 20, 30];
}
