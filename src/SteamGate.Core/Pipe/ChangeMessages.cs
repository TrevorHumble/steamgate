using SteamGate.Core.Rules;

namespace SteamGate.Core.Pipe;

/// <summary>Turns pipe messages into engine input and engine outcomes into pipe replies, so the engine itself never reads a pipe message. (SettingsCommand still carries the connection's OutboundQueue.)</summary>
public static class ChangeMessages
{
    /// <summary>
    /// Reads a pipe request into a ChangeRequest. Null when the message carries days that are not all day names; the caller
    /// then answers "Not allowed." without calling the engine.
    /// </summary>
    public static ChangeRequest? ToRequest(RequestMessage message)
    {
        IReadOnlyList<DayOfWeek>? days = null;
        if (message.Days is not null && !DayNames.TryParse(message.Days, out days))
        {
            return null;
        }

        return new ChangeRequest(message.Field, message.Number, message.Flag, message.Folder, days);
    }

    /// <summary>The reply for an outcome. When the save failed and the outcome changed something, the text says it is held in memory.</summary>
    public static ReplyMessage Reply(string requestId, ChangeOutcome outcome, bool saveFailed) =>
        new(requestId, outcome.Ok, saveFailed && outcome.Changed ? outcome.Text + ChangeReply.SaveFailedSuffix : outcome.Text, outcome.PendingId);

    /// <summary>The reply for a command the server could not queue or could not read.</summary>
    public static ReplyMessage Failure(string requestId, string text) => new(requestId, false, text, null);
}
