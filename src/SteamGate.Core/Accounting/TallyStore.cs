namespace SteamGate.Core.Accounting;

/// <summary>Reads and writes tally.json in the SteamGate data folder.</summary>
public sealed class TallyStore
{
    private const string FileName = "tally.json";

    private readonly JsonFileStore<Tally> _store;

    public TallyStore(string folder)
    {
        _store = new JsonFileStore<Tally>(folder, FileName, Tally.Empty, IsValid);
    }

    public LoadResult<Tally> Load() => _store.Load();

    /// <summary>Returns false, leaving the file unchanged, for a value Load would call corrupt (a negative or non-finite count, or an extension that is not 10, 20, or 30 minutes) or when the disk write keeps failing.</summary>
    public bool Save(Tally tally) => _store.Save(tally);

    // A count below 0 or without a finite value has no meaning as seconds. An extension is one of the allowed lengths.
    private static bool IsValid(Tally tally) =>
        double.IsFinite(tally.NightSeconds) && tally.NightSeconds >= 0
        && double.IsFinite(tally.WeekSeconds) && tally.WeekSeconds >= 0
        && (tally.Extension is null || NightExtension.Lengths.Contains(tally.Extension.Minutes));
}
