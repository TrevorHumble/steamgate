using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SteamGate.Core.Pipe;

/// <summary>
/// The messages waiting to be written to one connection. The tick posts to it and never writes; the connection's own task
/// reads from it and writes. A post that fails (the queue is full or closed) cancels the connection's cancel source, so the connection ends.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "It is a queue of messages that waits to be written; the name is the one the design uses.")]
public sealed class OutboundQueue
{
    public const int Capacity = 16;

    private readonly Channel<PipeMessage> _channel = Channel.CreateBounded<PipeMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    private readonly CancellationTokenSource _connection;

    /// <param name="connection">The cancel source the connection passes to every pipe read and write. A failed post cancels it.</param>
    public OutboundQueue(CancellationTokenSource connection)
    {
        _connection = connection;
    }

    /// <summary>False when the queue is full or closed; the connection is then cancelled (a source the connection already disposed is left alone).</summary>
    public bool TryPost(PipeMessage message)
    {
        if (_channel.Writer.TryWrite(message))
        {
            return true;
        }

        try
        {
            _connection.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The connection ended and disposed its source before this late post.
        }

        return false;
    }

    /// <summary>Waits for the next message. Throws ChannelClosedException after Complete once the queue is empty.</summary>
    public ValueTask<PipeMessage> ReadAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);

    /// <summary>Stops accepting posts.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
