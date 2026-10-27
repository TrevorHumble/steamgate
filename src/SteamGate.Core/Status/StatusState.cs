using SteamGate.Core.Accounting;
using SteamGate.Core.Pipe;
using SteamGate.Core.Steam;

namespace SteamGate.Core.Status;

/// <summary>Builds the state the status screen shows from the worker's snapshot, and reads it back into settings. The names on the screen all come from DisplayName.</summary>
public static class StatusState
{
    /// <summary>The most programs the add box can offer.</summary>
    public const int MaxAddable = 512;

    /// <summary>The longest display name in a state, in UTF-16 units.</summary>
    public const int MaxNameLength = 128;

    /// <summary>
    /// The state for this snapshot at this local time, or null while the snapshot has no settings or no time left computed yet.
    /// The tally is rolled over to tonight and this week first, so a tally from an earlier night or week shows 0 used.
    /// The add list is the installed programs not on the not-counted list, in name order, cut to fit StateMessage.MaxLineBytes
    /// by serialized size: it keeps the longest prefix of the sorted list that fits.
    /// </summary>
    public static StateMessage? From(GateSnapshot snapshot, DateTime localNow)
    {
        if (snapshot.Settings is not { } settings || snapshot.RemainingSeconds is null)
        {
            return null;
        }

        var tally = NightClock.RollOver(snapshot.Tally, localNow);
        var state = new StateMessage(
            snapshot.RemainingSeconds,
            snapshot.TonightLimitMinutes,
            settings.WeekendNights.Contains(NightClock.NightOf(localNow).DayOfWeek),
            tally.NightSeconds,
            tally.WeekSeconds,
            snapshot.Games.Count > 0 ? CutName(snapshot.Games[0].DisplayName) : null,
            snapshot.HelperConnected,
            SettingsOf(settings, snapshot.Installed),
            [],
            tally.ExtensionMinutes);

        var onList = new HashSet<string>(settings.NotCounted, StringComparer.OrdinalIgnoreCase);
        var addable = snapshot.Installed
            .Where(app => !onList.Contains(app.Folder) && IsWellFormed(app.Folder))
            .Select(app => new NamedFolder(app.Folder, ShownName(app.Folder, app.DisplayName)))
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Folder, StringComparer.OrdinalIgnoreCase)
            .Take(MaxAddable);

        // One pass: the line without the add list, then each entry's own size, until the next one would not fit.
        var total = System.Text.Encoding.UTF8.GetByteCount(state.Serialize());
        var kept = new List<NamedFolder>();
        foreach (var entry in addable)
        {
            var cost = StateMessage.EntryBytes(entry) + (kept.Count > 0 ? 1 : 0);
            if (total + cost > StateMessage.MaxLineBytes)
            {
                break;
            }

            total += cost;
            kept.Add(entry);
        }

        return state with { Addable = kept };
    }

    /// <summary>The rules as a state carries them: the settings without their countdown bookkeeping, the not-counted folders named for the screen.</summary>
    public static StateSettings SettingsOf(GateSettings settings, IReadOnlyList<InstalledApp> installed) => new(
        settings.WeeknightMinutes,
        settings.WeekendNightMinutes,
        settings.WeeklyCapMinutes,
        settings.CooldownHours,
        settings.Enabled,
        settings.AllowUninstall,
        settings.WeekendNights,
        [.. settings.NotCounted.Select(folder => new NamedFolder(folder, DisplayName(folder, installed)))],
        settings.Pending);

    /// <summary>The settings a state describes, with its pending changes and without the countdown bookkeeping. The status screen uses them for its dry run.</summary>
    public static GateSettings ToSettings(StateMessage state) => new()
    {
        WeeknightMinutes = state.Settings.WeeknightMinutes,
        WeekendNightMinutes = state.Settings.WeekendNightMinutes,
        WeeklyCapMinutes = state.Settings.WeeklyCapMinutes,
        CooldownHours = state.Settings.CooldownHours,
        WeekendNights = state.Settings.WeekendNights,
        NotCounted = [.. state.Settings.NotCounted.Select(entry => entry.Folder)],
        Enabled = state.Settings.Enabled,
        AllowUninstall = state.Settings.AllowUninstall,
        Pending = state.Settings.Pending,
        Clock = null,
    };

    /// <summary>
    /// The name to show for a folder: the approved name for one of the four approved programs (installed or not), else the installed program's
    /// name (the folder compared without regard to case), else the folder name. Cut to MaxNameLength without splitting a character.
    /// </summary>
    public static string DisplayName(string folder, IReadOnlyList<InstalledApp> installed) =>
        ShownName(folder, installed.FirstOrDefault(candidate => string.Equals(candidate.Folder, folder, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? folder);

    // The approved name for an approved folder, else the fallback; either way cut to length.
    private static string ShownName(string folder, string fallback)
    {
        foreach (var (approvedFolder, shown) in GateSettings.ApprovedNames)
        {
            if (string.Equals(approvedFolder, folder, StringComparison.OrdinalIgnoreCase))
            {
                return CutName(shown);
            }
        }

        return CutName(fallback);
    }

    /// <summary>
    /// The name to show for a folder on the screen: the approved name for an approved program, else the name the state carries for it
    /// (on the add list or the not-counted list, the folder compared without regard to case), else the folder name.
    /// </summary>
    public static string DisplayName(string folder, StateMessage state) =>
        ShownName(
            folder,
            state.Addable.Concat(state.Settings.NotCounted).FirstOrDefault(entry => string.Equals(entry.Folder, folder, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? folder);

    // A folder name that is not well-formed text (a lone half of a surrogate pair) cannot be sent as JSON, so it is never offered.
    private static bool IsWellFormed(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index++;
            }
            else if (char.IsSurrogate(text[index]))
            {
                return false;
            }
        }

        return true;
    }

    // At most MaxNameLength units; when the cut would end on the first half of a surrogate pair, one unit earlier.
    private static string CutName(string name)
    {
        if (name.Length <= MaxNameLength)
        {
            return name;
        }

        return char.IsHighSurrogate(name[MaxNameLength - 1]) ? name[..(MaxNameLength - 1)] : name[..MaxNameLength];
    }
}
