namespace SteamGate.Core.Rules;

/// <summary>The kind of value a field carries; it decides which one value member of a request or pending entry is set.</summary>
public enum FieldKind
{
    /// <summary>A whole number (Number member).</summary>
    Number,

    /// <summary>True or false (Flag member).</summary>
    Flag,

    /// <summary>One folder name (Folder member).</summary>
    Folder,

    /// <summary>A set of days (Days member).</summary>
    Days,
}

/// <summary>
/// Everything the lock knows about one field, in one place: its name and kind, whether a client may request it and whether it can wait as a
/// pending entry, and for number and flag fields its range, setters, and which direction loosens a rule (for folder and day fields that direction lives in the engine). The engine, the pending-entry check, and the settings check
/// all read this, so a new field cannot be added to one and missed in another.
/// </summary>
public sealed class FieldSpec
{
    internal FieldSpec(string name, FieldKind kind, bool requestable, bool pendable)
    {
        Name = name;
        Kind = kind;
        Requestable = requestable;
        Pendable = pendable;
    }

    public string Name { get; }

    public FieldKind Kind { get; }

    /// <summary>True when a client may name this field in a request.</summary>
    public bool Requestable { get; }

    /// <summary>True when this field can be the Field of a pending entry.</summary>
    public bool Pendable { get; }

    /// <summary>Number fields: the highest allowed value (the lowest is 0).</summary>
    public int Max { get; internal init; }

    /// <summary>Number fields: true when a higher value loosens the rule (a limit), false when a lower one does (the cooldown).</summary>
    public bool HigherLoosens { get; internal init; }

    /// <summary>Flag fields: the value that loosens the rule (enabled false, allowUninstall true).</summary>
    public bool LoosensWhenTrue { get; internal init; }

    internal Func<GateSettings, int>? GetNumber { get; init; }

    internal Func<GateSettings, int, GateSettings>? SetNumber { get; init; }

    internal Func<GateSettings, bool>? GetFlag { get; init; }

    internal Func<GateSettings, bool, GateSettings>? SetFlag { get; init; }

    /// <summary>True when exactly the one value member that matches the kind is set.</summary>
    public bool HasOnlyMember(int? number, bool? flag, string? folder, IReadOnlyList<DayOfWeek>? days) =>
        (number is not null) == (Kind == FieldKind.Number)
        && (flag is not null) == (Kind == FieldKind.Flag)
        && (folder is not null) == (Kind == FieldKind.Folder)
        && (days is not null) == (Kind == FieldKind.Days);

    public bool InRange(int value) => value >= 0 && value <= Max;

    /// <summary>True when the member is the right one and its value is allowed. A days entry must hold at least one day.</summary>
    public bool IsValidValue(int? number, bool? flag, string? folder, IReadOnlyList<DayOfWeek>? days) =>
        HasOnlyMember(number, flag, folder, days)
        && Kind switch
        {
            FieldKind.Number => InRange(number!.Value),
            FieldKind.Flag => true,
            FieldKind.Folder => ChangeFields.IsValidFolder(folder),
            _ => days!.Count > 0 && ChangeFields.AreDistinctDays(days),
        };
}

/// <summary>The table of fields and the fixed limits on their values.</summary>
public static class ChangeFields
{
    public const string WeeknightMinutes = "weeknightMinutes";
    public const string WeekendNightMinutes = "weekendNightMinutes";
    public const string WeeklyCapMinutes = "weeklyCapMinutes";
    public const string CooldownHours = "cooldownHours";
    public const string WeekendNights = "weekendNights";
    public const string NotCountedAdd = "notCountedAdd";
    public const string NotCountedRemove = "notCountedRemove";
    public const string Enabled = "enabled";
    public const string AllowUninstall = "allowUninstall";

    /// <summary>Pending-only: weekend nights that wait to be added.</summary>
    public const string WeekendNightsAdd = "weekendNightsAdd";

    /// <summary>Pending-only: weekend nights that wait to be taken out.</summary>
    public const string WeekendNightsRemove = "weekendNightsRemove";

    public const int MaxNightMinutes = 1440;
    public const int MaxWeeklyCapMinutes = 10080;

    /// <summary>The cooldown's hard maximum: 7 days.</summary>
    public const int MaxCooldownHours = 168;

    public const int MaxPending = 50;
    public const int MaxNotCounted = 50;
    public const int MaxFolderLength = 260;

    /// <summary>The longest a pending entry can wait: the longest cooldown, in seconds.</summary>
    public const double MaxWaitSeconds = MaxCooldownHours * 3600.0;

    private static readonly char[] FolderForbidden = ['\\', '/', ':'];

    /// <summary>Every field the engine knows.</summary>
    public static IReadOnlyList<FieldSpec> All { get; } =
    [
        new(WeeknightMinutes, FieldKind.Number, requestable: true, pendable: true)
        {
            Max = MaxNightMinutes,
            HigherLoosens = true,
            GetNumber = s => s.WeeknightMinutes,
            SetNumber = (s, value) => s with { WeeknightMinutes = value },
        },
        new(WeekendNightMinutes, FieldKind.Number, requestable: true, pendable: true)
        {
            Max = MaxNightMinutes,
            HigherLoosens = true,
            GetNumber = s => s.WeekendNightMinutes,
            SetNumber = (s, value) => s with { WeekendNightMinutes = value },
        },
        new(WeeklyCapMinutes, FieldKind.Number, requestable: true, pendable: true)
        {
            Max = MaxWeeklyCapMinutes,
            HigherLoosens = true,
            GetNumber = s => s.WeeklyCapMinutes,
            SetNumber = (s, value) => s with { WeeklyCapMinutes = value },
        },
        new(CooldownHours, FieldKind.Number, requestable: true, pendable: true)
        {
            Max = MaxCooldownHours,
            HigherLoosens = false,
            GetNumber = s => s.CooldownHours,
            SetNumber = (s, value) => s with { CooldownHours = value },
        },
        new(Enabled, FieldKind.Flag, requestable: true, pendable: true)
        {
            LoosensWhenTrue = false,
            GetFlag = s => s.Enabled,
            SetFlag = (s, value) => s with { Enabled = value },
        },
        new(AllowUninstall, FieldKind.Flag, requestable: true, pendable: true)
        {
            LoosensWhenTrue = true,
            GetFlag = s => s.AllowUninstall,
            SetFlag = (s, value) => s with { AllowUninstall = value },
        },
        new(NotCountedAdd, FieldKind.Folder, requestable: true, pendable: true),
        new(NotCountedRemove, FieldKind.Folder, requestable: true, pendable: false),
        new(WeekendNights, FieldKind.Days, requestable: true, pendable: false),
        new(WeekendNightsAdd, FieldKind.Days, requestable: false, pendable: true),
        new(WeekendNightsRemove, FieldKind.Days, requestable: false, pendable: true),
    ];

    /// <summary>The field with this name, or null for a name the engine does not know.</summary>
    public static FieldSpec? Find(string? name) => name is null ? null : All.FirstOrDefault(spec => spec.Name == name);

    /// <summary>The field name when the engine knows it, else "unknown", so client-supplied text never reaches the log.</summary>
    public static string LogName(string? field) => Find(field)?.Name ?? "unknown";

    /// <summary>True when a pending entry may carry this field.</summary>
    public static bool IsPendingField(string? field) => Find(field)?.Pendable == true;

    /// <summary>True when every number setting is inside its allowed range.</summary>
    public static bool NumbersInRange(GateSettings settings) =>
        All.Where(spec => spec.Kind == FieldKind.Number).All(spec => spec.InRange(spec.GetNumber!(settings)));

    /// <summary>True for a folder name that is 1 to 260 characters with no backslash, slash, or colon.</summary>
    public static bool IsValidFolder(string? folder) =>
        folder is { Length: >= 1 and <= MaxFolderLength } && folder.IndexOfAny(FolderForbidden) < 0;

    /// <summary>True when every item is a real day and no day repeats.</summary>
    public static bool AreDistinctDays(IReadOnlyList<DayOfWeek> days) =>
        days.All(day => Enum.IsDefined(day)) && days.Distinct().Count() == days.Count;
}
