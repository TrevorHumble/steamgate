using System.Text;

namespace SteamGate.Core.Pipe;

/// <summary>Reads one newline-ended line from a stream, never more bytes than the cap allows.</summary>
public static class PipeLineReader
{
    /// <summary>The longest line the pipe accepts, in bytes, not counting the newline.</summary>
    public const int MaxLineBytes = 16 * 1024;

    /// <summary>
    /// The line without its newline (and without a closing carriage return), or null when the stream ends before a
    /// newline or the line passes maxBytes. It reads one byte at a time, so it never takes bytes that belong to the next line,
    /// and it reads at most maxBytes + 1 bytes.
    /// </summary>
    public static async Task<string?> ReadLineAsync(Stream stream, int maxBytes, CancellationToken cancellationToken = default)
    {
        var line = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            if (one[0] == (byte)'\n')
            {
                if (line.Count > 0 && line[^1] == (byte)'\r')
                {
                    line.RemoveAt(line.Count - 1);
                }

                return Encoding.UTF8.GetString([.. line]);
            }

            if (line.Count >= maxBytes)
            {
                return null;
            }

            line.Add(one[0]);
        }
    }
}
