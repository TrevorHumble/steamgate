using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace SteamGate.Core.Pipe;

/// <summary>
/// The messages waiting to be written to one connection. The tick posts to it and never writes; the connection's own task
/// reads from it and writes. A full queue rejects the post, and the connection is then dropped.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "It is a queue of messages that waits to be written; the name is the one the design uses.")]
public sealed class OutboundQueue
{
    public const int Capacity = 16;

    private readonly Channel<PipeMessage> _channel = Channel.CreateBounded<PipeMessage>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    /// <summary>False when the queue is full or closed.</summary>
    public bool TryPost(PipeMessage message) => _channel.Writer.TryWrite(message);

    /// <summary>Waits for the next message. Throws ChannelClosedException after Complete once the queue is empty.</summary>
    public ValueTask<PipeMessage> ReadAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAsync(cancellationToken);

    /// <summary>Stops accepting posts.</summary>
    public void Complete() => _channel.Writer.TryComplete();
}
