using System.Globalization;

namespace SteamGate.Core.Rules;

/// <summary>
/// The service.log lines for the lock. Only fixed field names, results, and numbers appear: no client-supplied text, and never a folder
/// name, so a client cannot forge a line.
/// </summary>
public static class ChangeLog
{
    /// <summary>"request weeknightMinutes pending pid=12 session=1". An unknown field is written as "unknown".</summary>
    public static string Request(string? field, string result, int pid, int session) =>
        string.Create(CultureInfo.InvariantCulture, $"request {ChangeFields.LogName(field)} {result} pid={pid} session={session}");

    /// <summary>"cancel applied pid=12 session=1".</summary>
    public static string Cancel(string result, int pid, int session) =>
        string.Create(CultureInfo.InvariantCulture, $"cancel {result} pid={pid} session={session}");

    /// <summary>The result word a command the full channel turned away is logged with.</summary>
    public const string Busy = "busy";

    /// <summary>The lower-case name of a result, as logged.</summary>
    public static string ResultName(ChangeResult result) => result.ToString().ToLowerInvariant();

    /// <summary>"applied weeknightMinutes 120". The value is written for numbers, flags, and days; a folder name is never written.</summary>
    public static string Applied(PendingChange entry)
    {
        var field = ChangeFields.LogName(entry.Field);
        if (entry.Number is { } number)
        {
            return string.Create(CultureInfo.InvariantCulture, $"applied {field} {number}");
        }

        if (entry.Flag is { } flag)
        {
            return "applied " + field + " " + (flag ? "true" : "false");
        }

        if (entry.Days is { } days)
        {
            return "applied " + field + " " + string.Join(",", DayNames.MondayFirst(days));
        }

        return "applied " + field;
    }

    /// <summary>"skipped notCountedAdd".</summary>
    public static string Skipped(PendingChange entry) => "skipped " + ChangeFields.LogName(entry.Field);
}
