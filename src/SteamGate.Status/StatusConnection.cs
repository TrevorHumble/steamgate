using System.IO.Pipes;
using SteamGate.Core.Pipe;
using SteamGate.Core.Status;
using SteamGate.Helper;

namespace SteamGate.Status;

/// <summary>
/// The status screen's link to the service. A background task opens the pipe as the helper does (the helper's own PipeClient), says hello as the
/// status screen, and reads lines up to the state line limit. When the connection ends it tries again after the reconnect delay.
/// Messages and connection changes are reported on the background task; the window hands them to its own thread.
/// </summary>
internal sealed class StatusConnection : IDisposable
{
    private readonly Action<PipeMessage> _onMessage;
    private readonly Action<bool> _onConnected;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _writing = new(1, 1);
    private volatile NamedPipeClientStream? _current;
    private int _disposed;

    public StatusConnection(Action<PipeMessage> onMessage, Action<bool> onConnected)
    {
        _onMessage = onMessage;
        _onConnected = onConnected;
    }

    /// <summary>Starts connecting. Call once.</summary>
    public void Start() => _ = Task.Run(() => ConnectLoopAsync(_stopping.Token));

    /// <summary>
    /// Queues the message for writing. False when there is no open connection, in which case nothing is sent. A write that fails
    /// ends the connection, and the loop connects again.
    /// </summary>
    public bool TrySend(PipeMessage message)
    {
        var client = _current;
        if (client is null)
        {
            return false;
        }

        _ = WriteAsync(client, message.ToLine());
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stopping.Cancel();
        _current?.Dispose();
        _writing.Dispose();
        _stopping.Dispose();
    }

    private async Task WriteAsync(NamedPipeClientStream client, byte[] line)
    {
        try
        {
            await _writing.WaitAsync(_stopping.Token).ConfigureAwait(false);
            try
            {
                await client.WriteAsync(line, _stopping.Token).ConfigureAwait(false);
                await client.FlushAsync(_stopping.Token).ConfigureAwait(false);
            }
            finally
            {
                _writing.Release();
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // The connection is gone; ending it makes the read loop stop and connect again.
            client.Dispose();
        }
    }

    private async Task ConnectLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ServeOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Whatever went wrong, the screen stays alive and tries again after the reconnect delay, as the helper does.
            }

            try
            {
                await Task.Delay(StatusView.ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // One connection from hello to disconnect. Any failure ends it quietly; the loop connects again.
    private async Task ServeOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var client = PipeClient.TryConnect();
            if (client is null)
            {
                return;
            }

            await client.WriteAsync(new HelloMessage(HelloMessage.StatusRole).ToLine(), stoppingToken).ConfigureAwait(false);
            await client.FlushAsync(stoppingToken).ConfigureAwait(false);
            _current = client;
            _onConnected(true);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var line = await PipeLineReader.ReadLineAsync(client, StateMessage.MaxLineBytes, stoppingToken).ConfigureAwait(false);
                    if (PipeMessage.Parse(line) is not { } message)
                    {
                        return;
                    }

                    _onMessage(message);
                }
            }
            finally
            {
                _current = null;
                _onConnected(false);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException)
        {
            // The service closed the connection, went away, or the window is closing.
        }
    }
}
