using System.Diagnostics;

namespace SteamGate.Core.Accounting;

/// <summary>
/// Chooses the step and the cap for each count. The first call after the service starts gets the restart gap
/// and MaxRestartGap; every later call gets the time since the previous call and MaxTickStep.
/// </summary>
public sealed class StepTimer
{
    private readonly Stopwatch _sinceLastCall = new();
    private bool _firstCallDone;

    public (TimeSpan Step, TimeSpan MaxStep) Next(Tally loaded, DateTime utcNow, bool anyGameOpen)
    {
        var elapsed = _sinceLastCall.Elapsed;
        _sinceLastCall.Restart();

        if (_firstCallDone)
        {
            return (elapsed, Accountant.MaxTickStep);
        }

        _firstCallDone = true;
        return (Accountant.RestartStep(loaded, utcNow, anyGameOpen), Accountant.MaxRestartGap);
    }
}
