using SteamGate.Core.Pipe;

namespace SteamGate.Core.Rules;

/// <summary>
/// A command the worker takes from the pipe: a settings request or cancel, or an extend. Replies is the connection's outbound queue, never the connection itself;
/// the worker posts the answer there. The pid and session come from the pipe.
/// </summary>
public abstract record WorkerCommand
{
    private protected WorkerCommand(string requestId, OutboundQueue replies, int clientPid, int sessionId)
    {
        RequestId = requestId;
        Replies = replies;
        ClientPid = clientPid;
        SessionId = sessionId;
    }

    public string RequestId { get; }

    public OutboundQueue Replies { get; }

    public int ClientPid { get; }

    public int SessionId { get; }
}
