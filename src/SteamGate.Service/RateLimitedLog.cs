using System.Diagnostics;
using SteamGate.Core.Pipe;

namespace SteamGate.Service;

/// <summary>
/// Writes log lines for events a client can repeat in a loop (hello lines, request lines): at most 20 a minute, then one notice
/// a minute, so the loop cannot push earlier events out of the rotated log. Each kind of line has its own instance and budget.
/// </summary>
internal sealed class RateLimitedLog
{
    private readonly GateLog _log;
    private readonly string _suppressedNotice;
    private readonly LogLimiter _limiter = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public RateLimitedLog(GateLog log, string suppressedNotice)
    {
        _log = log;
        _suppressedNotice = suppressedNotice;
    }

    public void Write(string line)
    {
        switch (_limiter.Next(_clock.Elapsed))
        {
            case LogAction.Log:
                _log.Write(line);
                break;
            case LogAction.NoticeSuppressed:
                _log.Write(_suppressedNotice);
                break;
            default:
                break;
        }
    }
}
