using SteamGate.Core.Pipe;

namespace SteamGate.Core.Rules;

/// <summary>
/// One settings request or cancel from the pipe. Exactly one of Request or CancelId is set; the constructor throws ArgumentException otherwise.
/// </summary>
public sealed record SettingsCommand : WorkerCommand
{
    public SettingsCommand(string requestId, ChangeRequest? request, Guid? cancelId, OutboundQueue replies, int clientPid, int sessionId)
        : base(requestId, replies, clientPid, sessionId)
    {
        if ((request is null) == (cancelId is null))
        {
            throw new ArgumentException("Exactly one of request or cancelId must be set.");
        }

        Request = request;
        CancelId = cancelId;
    }

    public ChangeRequest? Request { get; }

    public Guid? CancelId { get; }
}
