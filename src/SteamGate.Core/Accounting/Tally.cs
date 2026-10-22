namespace SteamGate.Core.Accounting;

/// <summary>
/// The seconds counted in one night and one week, saved as tally.json.
/// LastCountedUtc is the time of the last count made while a game was open, and null when no game was open at the last count.
/// </summary>
public sealed record Tally(DateOnly Night, double NightSeconds, DateOnly Week, double WeekSeconds, DateTime? LastCountedUtc)
{
    public static Tally Empty { get; } = new(default, 0, default, 0, null);
}
