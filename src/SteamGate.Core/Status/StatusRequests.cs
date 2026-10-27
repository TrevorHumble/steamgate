using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Status;

/// <summary>
/// Builds the messages the status screen sends. The screen turns the same RequestMessage into a ChangeRequest with
/// ChangeMessages.ToRequest for its dry run, the call the service makes, so it judges what the service will judge.
/// </summary>
public static class StatusRequests
{
    /// <summary>The id of the nth request: "s1", "s2", and so on, always far under the 64-character limit.</summary>
    public static string Id(int sequence) => "s" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static RequestMessage Number(string requestId, string field, int value) => new(requestId, field, value, null, null, null);

    public static RequestMessage Flag(string requestId, string field, bool value) => new(requestId, field, null, value, null, null);

    public static RequestMessage Folder(string requestId, string field, string folder) => new(requestId, field, null, null, folder, null);

    /// <summary>The weekend-nights request: the days as English names, Monday first.</summary>
    public static RequestMessage WeekendNights(string requestId, IEnumerable<DayOfWeek> days) =>
        new(requestId, ChangeFields.WeekendNights, null, null, null, [.. DayNames.MondayFirst(days).Select(day => day.ToString())]);

    public static ExtendMessage Extend(string requestId, int minutes) => new(requestId, minutes);

    public static CancelMessage Cancel(string requestId, Guid pendingId) => new(requestId, pendingId);
}
