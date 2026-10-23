using SteamGate.Core.Pipe;

namespace SteamGate.Core.Rules;

/// <summary>
/// What the pipe server hands the worker for one request or cancel. Exactly one of Request or CancelId is set. Replies is the
/// connection's outbound queue, never the connection itself; the worker posts the answer there. The pid and session come from the pipe.
/// </summary>
public sealed record SettingsCommand(string RequestId, ChangeRequest? Request, Guid? CancelId, OutboundQueue Replies, int ClientPid, int SessionId);
