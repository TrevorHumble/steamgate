namespace SteamGate.Core.Pipe;

/// <summary>A client that has said hello. Lists of these are ordered oldest first.</summary>
public sealed record ClientInfo(long Id, bool VerifiedHelper);

/// <summary>Whether a new client joins, and which older client (by Id) leaves to make room.</summary>
public sealed record AdmitDecision(bool Admit, long? EvictId);

/// <summary>The pipe server's connection decisions. PipeServer only applies the answers.</summary>
public static class ConnectionRules
{
    /// <summary>The most clients connected at once; the pipe allows one more instance, kept listening.</summary>
    public const int MaxClients = 8;

    /// <summary>How long a new connection has to send its hello; the server's read limit uses this value.</summary>
    public static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(10);

    /// <summary>How long a connection that is not a verified helper may go without sending a complete line before it is dropped.</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(10);

    /// <summary>The longest requestId a client may send.</summary>
    public const int MaxRequestIdLength = 64;

    /// <summary>The hello when the first line is one; null means the connection is dropped (a first line that is anything else).</summary>
    public static HelloMessage? AcceptedHello(PipeMessage? first) => first as HelloMessage;

    /// <summary>
    /// After the hello a client may send only request and cancel, each with a requestId of at most 64 characters. A warn, a close, a reply
    /// (the server alone sends those), and a second hello end the connection.
    /// </summary>
    public static bool DropsOnClientMessage(PipeMessage message) => message switch
    {
        RequestMessage request => request.RequestId.Length > MaxRequestIdLength,
        CancelMessage cancel => cancel.RequestId.Length > MaxRequestIdLength,
        _ => true,
    };

    /// <summary>How long a connection may be silent: null for a verified helper, which stays connected; the idle limit for anyone else.</summary>
    public static TimeSpan? IdleLimitFor(bool verifiedHelper) => verifiedHelper ? null : IdleLimit;

    /// <summary>
    /// Below MaxClients everyone joins. At MaxClients a verified helper takes the place of the oldest client that is not
    /// a verified helper; anyone else, or a helper when every client is a verified helper, is dropped.
    /// </summary>
    public static AdmitDecision Admit(IReadOnlyList<ClientInfo> connectedOldestFirst, bool newIsVerifiedHelper)
    {
        if (connectedOldestFirst.Count < MaxClients)
        {
            return new AdmitDecision(true, null);
        }

        if (newIsVerifiedHelper)
        {
            var oldestUnverified = connectedOldestFirst.FirstOrDefault(client => !client.VerifiedHelper);
            if (oldestUnverified is not null)
            {
                return new AdmitDecision(true, oldestUnverified.Id);
            }
        }

        return new AdmitDecision(false, null);
    }
}
