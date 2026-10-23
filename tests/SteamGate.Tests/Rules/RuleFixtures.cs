using SteamGate.Core;
using SteamGate.Core.Rules;

namespace SteamGate.Tests.Rules;

/// <summary>Shared builders for the lock's tests. The default test time is 2026-10-20T21:00:00Z and the default cooldown is 48 h.</summary>
internal static class RuleFixtures
{
    public const double CooldownSeconds = 172800;

    public static readonly DateTime Now = new(2026, 10, 20, 21, 0, 0, DateTimeKind.Utc);

    /// <summary>A source of ids that counts up from 1, so a test can predict them.</summary>
    public static Func<Guid> Ids()
    {
        var next = 0;
        return () => new Guid(++next, 0, 0, new byte[8]);
    }

    public static Guid IdNumber(int number) => new(number, 0, 0, new byte[8]);

    public static ChangeRequest Number(string field, int value) => new(field, value, null, null, null);

    public static ChangeRequest Flag(string field, bool value) => new(field, null, value, null, null);

    public static ChangeRequest Folder(string field, string value) => new(field, null, null, value, null);

    public static ChangeRequest Days(params DayOfWeek[] days) => new(ChangeFields.WeekendNights, null, null, null, days);

    public static ChangeOutcome Ask(GateSettings settings, ChangeRequest request) => ChangeEngine.Request(settings, request, Now, Ids());

    public static PendingChange NumberEntry(string field, int value, double remaining = CooldownSeconds, int id = 1, DateTime? requested = null) =>
        new(IdNumber(id), field, value, null, null, null, requested ?? Now, remaining);

    public static PendingChange FlagEntry(string field, bool value, double remaining = CooldownSeconds, int id = 1) =>
        new(IdNumber(id), field, null, value, null, null, Now, remaining);

    public static PendingChange FolderEntry(string folder, double remaining = CooldownSeconds, int id = 1) =>
        new(IdNumber(id), ChangeFields.NotCountedAdd, null, null, folder, null, Now, remaining);

    public static PendingChange DaysEntry(string field, double remaining, int id, params DayOfWeek[] days) =>
        new(IdNumber(id), field, null, null, null, days, Now, remaining);

    public static GateSettings With(this GateSettings settings, params PendingChange[] pending) => settings with { Pending = pending };

    /// <summary>Fills the pending list with valid entries that change nothing a test looks at.</summary>
    public static PendingChange[] Filler(int count, int firstId = 100) =>
        [.. Enumerable.Range(0, count).Select(index => NumberEntry(ChangeFields.WeeklyCapMinutes, 700 + index, id: firstId + index))];

    public static string[] Folders(int count) => [.. Enumerable.Range(0, count).Select(index => "Folder" + index)];
}
