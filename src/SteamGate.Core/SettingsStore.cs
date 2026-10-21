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

    /// <summary>Returns false for a value Load would call corrupt (a null list item or a negative limit), leaving the file unchanged.</summary>
    public bool Save(GateSettings settings) => _store.Save(settings);

    // The deserializer rejects null lists, but a caller can pass one to Save, so check both lists first.
    // A null item inside NotCounted is the case the deserializer lets through.
    // A negative limit is corrupt too: it has no meaning as minutes or hours.
    private static bool IsValid(GateSettings settings) =>
        settings.NotCounted is not null
        && settings.WeekendNights is not null
        && settings.NotCounted.All(name => name is not null)
        && settings.WeeknightMinutes >= 0
        && settings.WeekendNightMinutes >= 0
        && settings.WeeklyCapMinutes >= 0
        && settings.CooldownHours >= 0;
}
