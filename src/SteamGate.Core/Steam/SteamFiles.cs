using System.Security;
using System.Text;

namespace SteamGate.Core.Steam;

/// <summary>Reads Steam's own files, which any standard user can change, so every failure becomes a result and never an exception.</summary>
public static class SteamFiles
{
    /// <summary>The largest vdf or acf file the service reads, in bytes.</summary>
    public const int MaxVdfBytes = 1024 * 1024;

    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Text is null for a missing, locked, unreadable, or oversized (more than limit bytes, at most the 1 MB cap) file.
    /// Locked is true only when the file exists but could not be opened (IOException or UnauthorizedAccessException),
    /// so the caller can keep what it read last time.
    /// </summary>
    public static (string? Text, bool Locked) ReadCapped(string path, long limit = MaxVdfBytes) =>
        ReadCapped(path, limit, out _);

    /// <summary>
    /// The same read, and bytesRead is how many bytes of the file were actually read, never more than limit, so a caller
    /// can charge a budget for what it took even when the file grew after it was measured.
    /// </summary>
    public static (string? Text, bool Locked) ReadCapped(string path, long limit, out long bytesRead)
    {
        bytesRead = 0;
        var cap = (int)Math.Clamp(limit, 0, MaxVdfBytes);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // One byte over the limit is enough to tell an oversized file from one exactly at the limit.
            var buffer = new byte[(int)Math.Min(stream.Length, cap + 1L)];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = stream.Read(buffer, total, buffer.Length - total);
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            bytesRead = Math.Min(total, cap);
            if (total > cap)
            {
                return (null, false);
            }

            var start = buffer.AsSpan(0, Math.Min(total, Utf8Bom.Length)).SequenceEqual(Utf8Bom) ? Utf8Bom.Length : 0;
            return (Encoding.UTF8.GetString(buffer, start, total - start), false);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, true);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or SecurityException)
        {
            return (null, false);
        }
    }
}
