using SteamGate.Core.Pipe;

namespace SteamGate.Core.Rules;

/// <summary>An extend request from the pipe. The length is whatever the client sent; the worker checks it.</summary>
public sealed record ExtendCommand(string RequestId, int Minutes, OutboundQueue Replies, int ClientPid, int SessionId)
    : WorkerCommand(RequestId, Replies, ClientPid, SessionId);
