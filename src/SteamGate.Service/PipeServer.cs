using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;
using SteamGate.Core;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;

namespace SteamGate.Service;

/// <summary>
/// The pipe \\.\pipe\SteamGate: how the helper and the status screen talk to the service. It is a convenience
/// and must never stop enforcement. Every failure here is logged and isolated; the worker retries through EnsureListening
/// on each tick. The decisions (who may join, who is dropped, who gets warnings) are in ConnectionRules; this class applies them.
/// </summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the service container.")]
internal sealed class PipeServer : IHostedService, IDisposable
{
    internal const int MaxInstances = ConnectionRules.MaxClients + 1;
    private const int BufferSize = 4096;
    private static readonly TimeSpan UnavailableLogInterval = TimeSpan.FromMinutes(1);

    private readonly GateLog _log;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _lock = new();
    private readonly List<Connection> _clients = [];
    private readonly SemaphoreSlim _released = new(0);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ChannelWriter<WorkerCommand> _commands;
    private readonly RateLimitedLog _helloLog;
    private readonly RateLimitedLog _requestLog;

    // Which state the status screens get and what a new one is sent first. Touched only under _lock.
    private readonly StatusFeed _feed = new();
    private int _activeConnections;
    private Task? _loop;
    private int _openInstances;
    private long _nextId;
    private string _lastUnavailable = string.Empty;
    private TimeSpan _lastUnavailableAt = TimeSpan.MinValue;

    public PipeServer(GateLog log, Channel<WorkerCommand> commands, RateLimitedLog requestLog)
    {
        _log = log;
        _commands = commands.Writer;
        _requestLog = requestLog;
        _helloLog = new RateLimitedLog(log, "pipe hello suppressed");
    }

    /// <summary>True when a connected client is a verified helper.</summary>
    public bool HelperConnected
    {
        get
        {
            lock (_lock)
            {
                return _clients.Any(client => client.Verified);
            }
        }
    }

    /// <summary>True when at least one connection said hello as the status screen.</summary>
    public bool StatusConnected
    {
        get
        {
            lock (_lock)
            {
                return AnyStatusClient();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        return Task.CompletedTask;
    }

    // Cancel first, wait for the accept loop and the connection tasks to finish with the token, and only then dispose it.
    public void Dispose()
    {
        _stopping.Cancel();
        Task? loop;
        lock (_lock)
        {
            loop = _loop;
        }

        if (SpinWait.SpinUntil(() => (loop is null || loop.IsCompleted) && Volatile.Read(ref _activeConnections) == 0, TimeSpan.FromSeconds(2)))
        {
            _stopping.Dispose();
        }
    }

    /// <summary>Starts the accept loop if none is running. Never throws. Called on every tick, which is the only retry.</summary>
    public void EnsureListening()
    {
        try
        {
            lock (_lock)
            {
                if (_stopping.IsCancellationRequested || _loop is { IsCompleted: false })
                {
                    return;
                }

                _loop = Task.Run(RunAcceptLoopAsync);
            }
        }
        catch (ObjectDisposedException)
        {
            // The server is shutting down.
        }
    }

    /// <summary>How many verified helpers accepted the message into their queue. A helper whose queue is full is dropped. Never writes to a pipe.</summary>
    public int PostToHelpers(PipeMessage message)
    {
        var accepted = 0;
        var full = new List<Connection>();
        lock (_lock)
        {
            foreach (var client in _clients)
            {
                if (!client.Verified)
                {
                    continue;
                }

                if (client.Queue.TryPost(message))
                {
                    accepted++;
                }
                else
                {
                    full.Add(client);
                }
            }
        }

        foreach (var client in full)
        {
            client.Drop();
        }

        return accepted;
    }

    /// <summary>
    /// Offers a state to the status screens: it is posted to every status connection's queue when it differs from the last one posted,
    /// and never to a helper. With no status connection it is dropped. A screen whose queue is full is dropped. Never writes to a pipe.
    /// </summary>
    public void PublishState(StateMessage state)
    {
        var full = new List<Connection>();
        try
        {
            lock (_lock)
            {
                if (!AnyStatusClient() || !_feed.Offer(state))
                {
                    return;
                }

                foreach (var client in _clients.Where(client => client.IsStatus))
                {
                    if (!client.Queue.TryPost(state))
                    {
                        full.Add(client);
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            // The server is shutting down.
            return;
        }

        foreach (var client in full)
        {
            client.Drop();
        }
    }

    /// <summary>Checks again that each verified helper is the installed program in the console session; one that is not is dropped.</summary>
    public void RecheckHelpers()
    {
        Connection[] verified;
        lock (_lock)
        {
            verified = [.. _clients.Where(client => client.Verified)];
        }

        foreach (var client in verified)
        {
            if (!IsVerifiedHelper(client.Pid))
            {
                client.Verified = false;
                client.Drop();
            }
        }
    }

    // The whole loop carries the worker's last-resort handler; only a failure to create or accept an instance takes the "pipe unavailable" path.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Last-resort handler that logs and ends the process so Windows restarts it.")]
    internal async Task RunAcceptLoopAsync()
    {
        try
        {
            await AcceptLoopAsync(_stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && _stopping.IsCancellationRequested))
        {
            try
            {
                _log.Write(FormattableString.Invariant($"unhandled pid={Environment.ProcessId} {ex.Message}"));
            }
            finally
            {
                Environment.Exit(1);
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken stoppingToken)
    {
        NamedPipeServerStream? listening = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (listening is null)
                {
                    // At 9 of 9 there is no room for a listening instance until a connection ends.
                    while (Volatile.Read(ref _openInstances) >= MaxInstances)
                    {
                        await _released.WaitAsync(stoppingToken).ConfigureAwait(false);
                    }

                    listening = TryCreateInstance(out var createError);
                    if (listening is null)
                    {
                        LogUnavailable(createError);
                        return;
                    }
                }

                try
                {
                    await listening.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (IOException ex)
                {
                    // This client's accept failed. Keep the name by creating the replacement first; at 9 of 9 that is impossible.
                    var replacement = Volatile.Read(ref _openInstances) < MaxInstances ? TryCreateInstance(out _) : null;
                    DisposeInstance(listening);
                    listening = replacement;
                    if (listening is null)
                    {
                        LogUnavailable(ex.Message);
                        return;
                    }

                    continue;
                }

                var connected = listening;
                listening = null;
                Interlocked.Increment(ref _activeConnections);
                _ = Task.Run(() => HandleConnectionAsync(connected, stoppingToken), CancellationToken.None);
                listening = Volatile.Read(ref _openInstances) < MaxInstances ? TryCreateInstance(out _) : null;
            }
        }
        finally
        {
            if (listening is not null)
            {
                DisposeInstance(listening);
            }
        }
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The caller owns the returned instance and disposes it through DisposeInstance.")]
    private NamedPipeServerStream? TryCreateInstance(out string error)
    {
        try
        {
            // The first instance claims the name exclusively, so a squatter cannot be joined by accident.
            var options = PipeOptions.Asynchronous;
            if (Volatile.Read(ref _openInstances) == 0)
            {
                options |= PipeOptions.FirstPipeInstance;
            }

            var instance = NamedPipeServerStreamAcl.Create(
                PipeContract.PipeName,
                PipeDirection.InOut,
                MaxInstances,
                PipeTransmissionMode.Byte,
                options,
                BufferSize,
                BufferSize,
                BuildSecurity());
            Interlocked.Increment(ref _openInstances);
            error = string.Empty;
            return instance;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return null;
        }
    }

    private void DisposeInstance(NamedPipeServerStream instance)
    {
        instance.Dispose();
        Interlocked.Decrement(ref _openInstances);

        // The accept loop only needs to know that something ended, so the count never grows past one.
        if (_released.CurrentCount == 0)
        {
            _released.Release();
        }
    }

    private void LogUnavailable(string error)
    {
        var now = _clock.Elapsed;
        if (error == _lastUnavailable && now - _lastUnavailableAt < UnavailableLogInterval)
        {
            return;
        }

        _lastUnavailable = error;
        _lastUnavailableAt = now;
        _log.Write("pipe unavailable " + error);
    }

    // SYSTEM and Administrators full control. Authenticated users may read and write the pipe but never create an instance,
    // so no standard user can open a second server. The network is denied.
    private static PipeSecurity BuildSecurity()
    {
        // The same bits a client asks for (PipeContract.ClientAccessMask): read, write, read attributes and extended attributes, read permissions, synchronize.
        const PipeAccessRights clientRights = (PipeAccessRights)PipeContract.ClientAccessMask;

        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null), clientRights, AccessControlType.Allow));
        return security;
    }

    // One connection's whole life. An error here ends this connection only.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failure on one connection must not affect the others or enforcement.")]
    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken stoppingToken)
    {
        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var connection = new Connection(Interlocked.Increment(ref _nextId), connectionCts);
        try
        {
            var hello = await ReadHelloAsync(pipe, connectionCts.Token).ConfigureAwait(false);
            if (hello is null)
            {
                return;
            }

            connection.Pid = ClientProcessId(pipe);
            connection.SessionId = ClientSessionId(pipe);
            connection.IsStatus = hello.Role == HelloMessage.StatusRole;
            LogHello(hello.Role, connection.Pid);
            var verified = hello.Role == HelloMessage.HelperRole && IsVerifiedHelper(connection.Pid);

            // A helper that is not the installed program in the console session is dropped at once; it retries in 5 seconds.
            if ((hello.Role == HelloMessage.HelperRole && !verified) || !Register(connection, verified))
            {
                return;
            }

            var writer = WriteLoopAsync(pipe, connection);
            await ReadLoopAsync(pipe, connection).ConfigureAwait(false);
            connection.Drop();
            await writer.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (ex is not (OperationCanceledException or IOException or ObjectDisposedException))
            {
                _log.Write("pipe connection error " + ex.Message);
            }
        }
        finally
        {
            lock (_lock)
            {
                _clients.Remove(connection);

                // With the last status screen gone, the stored state goes too, so the next screen never gets an old one.
                if (!AnyStatusClient())
                {
                    _feed.Forget();
                }
            }

            connection.Queue.Complete();
            DisposeInstance(pipe);
            Interlocked.Decrement(ref _activeConnections);
        }
    }

    // The first line must be a hello and must arrive within the hello limit; anything else gives null.
    private static async Task<HelloMessage?> ReadHelloAsync(NamedPipeServerStream pipe, CancellationToken connectionToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        limit.CancelAfter(ConnectionRules.HelloTimeout);
        try
        {
            var line = await PipeLineReader.ReadLineAsync(pipe, PipeLineReader.MaxLineBytes, limit.Token).ConfigureAwait(false);
            var message = PipeMessage.Parse(line);
            return ConnectionRules.AcceptedHello(message);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // After the hello a line that is too long, malformed, unknown, or not allowed (ConnectionRules) ends the connection. A request, cancel, or extend
    // goes to the worker. A connection that is not a verified helper is dropped when no complete line arrives for the idle limit.
    private async Task ReadLoopAsync(NamedPipeServerStream pipe, Connection connection)
    {
        while (!connection.Token.IsCancellationRequested)
        {
            string? line;
            using (var idle = CancellationTokenSource.CreateLinkedTokenSource(connection.Token))
            {
                if (ConnectionRules.IdleLimitFor(connection.Verified) is { } limit)
                {
                    idle.CancelAfter(limit);
                }

                try
                {
                    line = await PipeLineReader.ReadLineAsync(pipe, PipeLineReader.MaxLineBytes, idle.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!connection.Token.IsCancellationRequested)
                {
                    // Idle too long.
                    return;
                }
            }

            var message = PipeMessage.Parse(line);
            if (message is null || ConnectionRules.DropsOnClientMessage(message))
            {
                return;
            }

            Submit(connection, message);
        }
    }

    // Hands a request, cancel, or extend to the worker through the channel. A full channel is answered at once; the server never waits for the worker.
    private void Submit(Connection connection, PipeMessage message)
    {
        var pid = (int)connection.Pid;
        switch (message)
        {
            case RequestMessage request when ChangeMessages.ToRequest(request) is { } change:
                if (!_commands.TryWrite(new SettingsCommand(request.RequestId, change, null, connection.Queue, pid, connection.SessionId)))
                {
                    _requestLog.Write(ChangeLog.Request(request.Field, ChangeLog.Busy, pid, connection.SessionId));
                    connection.Queue.TryPost(ChangeMessages.Failure(request.RequestId, ChangeReply.Busy));
                }

                break;
            case RequestMessage request:
                _requestLog.Write(ChangeLog.Request(request.Field, ChangeLog.ResultName(ChangeResult.Refused), pid, connection.SessionId));
                connection.Queue.TryPost(ChangeMessages.Failure(request.RequestId, ChangeReply.NotAllowed));
                break;
            case CancelMessage cancel:
                if (!_commands.TryWrite(new SettingsCommand(cancel.RequestId, null, cancel.Id, connection.Queue, pid, connection.SessionId)))
                {
                    _requestLog.Write(ChangeLog.Cancel(ChangeLog.Busy, pid, connection.SessionId));
                    connection.Queue.TryPost(ChangeMessages.Failure(cancel.RequestId, ChangeReply.Busy));
                }

                break;
            case ExtendMessage extend:
                if (!_commands.TryWrite(new ExtendCommand(extend.RequestId, extend.Minutes, connection.Queue, pid, connection.SessionId)))
                {
                    _requestLog.Write(ChangeLog.Extend(ChangeLog.Busy, pid, connection.SessionId));
                    connection.Queue.TryPost(ChangeMessages.Failure(extend.RequestId, ChangeReply.Busy));
                }

                break;
            default:
                break;
        }
    }

    private static async Task WriteLoopAsync(NamedPipeServerStream pipe, Connection connection)
    {
        try
        {
            while (true)
            {
                var message = await connection.Queue.ReadAsync(connection.Token).ConfigureAwait(false);
                await pipe.WriteAsync(message.ToLine(), connection.Token).ConfigureAwait(false);
                await pipe.FlushAsync(connection.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ChannelClosedException or IOException or ObjectDisposedException)
        {
            connection.Drop();
        }
    }

    // At most 20 hello lines a minute, then one notice a minute, so a client that reconnects in a loop cannot push earlier events out of the rotated log.
    private void LogHello(string role, uint pid) =>
        _helloLog.Write(FormattableString.Invariant($"pipe hello role={role} pid={pid}"));

    // True when a registered connection is a status screen. The caller holds _lock.
    private bool AnyStatusClient() => _clients.Any(client => client.IsStatus);

    private bool Register(Connection connection, bool verified)
    {
        Connection? evicted = null;
        lock (_lock)
        {
            var decision = ConnectionRules.Admit([.. _clients.Select(client => new ClientInfo(client.Id, client.Verified))], verified);
            if (!decision.Admit)
            {
                return false;
            }

            if (decision.EvictId is { } evictId)
            {
                evicted = _clients.Find(client => client.Id == evictId);
                if (evicted is not null)
                {
                    _clients.Remove(evicted);
                }
            }

            connection.Verified = verified;
            _clients.Add(connection);

            // A new status screen gets the latest state at once, ahead of anything posted later (PublishState posts under this lock too).
            if (connection.IsStatus && _feed.Latest is { } latest)
            {
                connection.Queue.TryPost(latest);
            }
        }

        evicted?.Drop();
        return true;
    }

    // A helper is verified when the client's program is the installed helper and it runs in the console session.
    // The server finds both itself and trusts nothing the client says.
    private static bool IsVerifiedHelper(uint pid)
    {
        if (pid == 0)
        {
            return false;
        }

        using var process = ProcessImage.OpenForQuery((int)pid);
        if (process.IsInvalid || !ProcessImage.TryReadPath(process, out var imagePath))
        {
            return false;
        }

        return string.Equals(Path.GetFullPath(imagePath), SteamGatePaths.HelperPath, StringComparison.OrdinalIgnoreCase)
            && ProcessIdToSessionId(pid, out var session)
            && ConsoleSession.ActiveId() == session;
    }

    private static uint ClientProcessId(NamedPipeServerStream pipe) =>
        GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var pid) ? pid : 0;

    private static int ClientSessionId(NamedPipeServerStream pipe) =>
        GetNamedPipeClientSessionId(pipe.SafePipeHandle, out var session) ? (int)session : -1;

    private sealed class Connection
    {
        private readonly CancellationTokenSource _cts;

        public Connection(long id, CancellationTokenSource cts)
        {
            Id = id;
            _cts = cts;
            Token = cts.Token;
            Queue = new OutboundQueue(cts);
        }

        public long Id { get; }

        public CancellationToken Token { get; }

        public OutboundQueue Queue { get; }

        public volatile bool Verified;

        /// <summary>True when the hello said "status". Set before the connection is registered, never changed after.</summary>
        public volatile bool IsStatus;

        public uint Pid { get; set; }

        public int SessionId { get; set; }

        public void Drop()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already gone.
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint clientSessionId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);
}
