namespace SteamGate.Core.Pipe;

/// <summary>What to write to the log for one valid hello.</summary>
public enum HelloLogAction
{
    /// <summary>Write the normal hello line.</summary>
    Log,

    /// <summary>Write one notice that further hello lines are being left out, and nothing more this minute.</summary>
    NoticeSuppressed,

    /// <summary>Write nothing.</summary>
    Skip,
}

/// <summary>
/// Keeps a client that reconnects in a loop from filling the rotated log: at most MaxPerMinute hello lines per minute, then one
/// notice per minute. Minutes are counted from the first hello after the previous minute ended.
/// </summary>
public sealed class HelloLogLimiter
{
    public const int MaxPerMinute = 20;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly object _lock = new();
    private TimeSpan? _windowStart;
    private int _logged;
    private bool _noticeWritten;

    /// <param name="now">A clock that never goes back, such as a Stopwatch reading.</param>
    public HelloLogAction Next(TimeSpan now)
    {
        lock (_lock)
        {
            if (_windowStart is null || now - _windowStart >= Window)
            {
                _windowStart = now;
                _logged = 0;
                _noticeWritten = false;
            }

            if (_logged < MaxPerMinute)
            {
                _logged++;
                return HelloLogAction.Log;
            }

            if (_noticeWritten)
            {
                return HelloLogAction.Skip;
            }

            _noticeWritten = true;
            return HelloLogAction.NoticeSuppressed;
        }
    }
}
