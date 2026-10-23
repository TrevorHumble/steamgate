using SteamGate.Core.Rules;

namespace SteamGate.Core;

/// <summary>Reads and writes settings.json in the SteamGate data folder.</summary>
public sealed class SettingsStore
{
    private const string FileName = "settings.json";

    private readonly JsonFileStore<GateSettings> _store;

    public SettingsStore(string folder)
    {
        _store = new JsonFileStore<GateSettings>(folder, FileName, GateSettings.Defaults, IsValid);
    }

    public LoadResult<GateSettings> Load() => _store.Load();

    /// <summary>
    /// Returns false for a value Load would call corrupt (a null list item, a folder name that breaks the folder rule, a repeated weekend night,
    /// a value outside its allowed range, more than 50 not-counted folders or pending entries, or a bad pending entry), leaving the file unchanged.
    /// </summary>
    public bool Save(GateSettings settings) => _store.Save(settings);

    // The deserializer rejects null lists, but a caller can pass one to Save, so check the lists first.
    // A null item inside NotCounted is the case the deserializer lets through; a folder name must also pass the folder rule.
    // A number outside its range is corrupt: a negative has no meaning as minutes or hours, and a hand edit must not set a cooldown past 7 days.
    // A pending entry must be one the engine could have made, and there can be at most 50 of them. The ranges come from the field table in ChangeFields.
    private static bool IsValid(GateSettings settings) =>
        settings.NotCounted is not null
        && settings.WeekendNights is not null
        && settings.Pending is not null
        && settings.NotCounted.Count <= ChangeFields.MaxNotCounted
        && settings.NotCounted.All(ChangeFields.IsValidFolder)
        && ChangeFields.AreDistinctDays(settings.WeekendNights)
        && settings.Pending.Count <= ChangeFields.MaxPending
        && settings.Pending.All(entry => entry is not null && entry.IsValid())
        && ChangeFields.NumbersInRange(settings);
}
