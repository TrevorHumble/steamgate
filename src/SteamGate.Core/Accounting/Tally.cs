namespace SteamGate.Core.Accounting;

/// <summary>
/// The seconds counted in one night and one week, saved as tally.json.
/// LastCountedUtc is the time of the last count made while a game was open, and null when no game was open at the last count.
/// Extension is the extension taken for the named night, or null; a roll over to another night drops it.
/// </summary>
public sealed record Tally(DateOnly Night, double NightSeconds, DateOnly Week, double WeekSeconds, DateTime? LastCountedUtc, NightExtension? Extension = null)
{
    public static Tally Empty { get; } = new(default, 0, default, 0, null);

    /// <summary>The minutes of tonight's extension, 0 when there is none.</summary>
    public int ExtensionMinutes => Extension?.Minutes ?? 0;

    /// <summary>True when there is an extension of more than 0 minutes. The service-side accounting (Budget and Accountant) asks this.</summary>
    public bool HasExtension => ExtensionMinutes > 0;
}
