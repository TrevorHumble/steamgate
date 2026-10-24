using SteamGate.Core.Status;

namespace SteamGate.Status;

/// <summary>
/// The requests the window has sent: how many (so the next id is new) and which section is waiting for each answer. The window keeps it
/// when it rebuilds the page, so an answer that arrives after a DPI change still lands.
/// </summary>
internal sealed class RequestLog
{
    private readonly Dictionary<string, ReplySection> _awaiting = [];
    private int _sequence;

    /// <summary>The id the next request will have, for building the message before it is known to be sent.</summary>
    public string PeekId() => StatusRequests.Id(_sequence + 1);

    /// <summary>Notes that a request was sent for this section, and returns its id. Call it only after the message was sent.</summary>
    public string Begin(ReplySection section)
    {
        _sequence++;
        var id = StatusRequests.Id(_sequence);
        _awaiting[id] = section;
        return id;
    }

    /// <summary>The section waiting for the answer to this request, which is then no longer waiting.</summary>
    public bool TryComplete(string requestId, out ReplySection section) => _awaiting.Remove(requestId, out section);
}
