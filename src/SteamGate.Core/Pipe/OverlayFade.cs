namespace SteamGate.Core.Pipe;

/// <summary>How visible the overlay is as time passes: fully for the hold, then a straight fade to nothing.</summary>
public static class OverlayFade
{
    public static readonly TimeSpan OverlayHold = TimeSpan.FromSeconds(8);

    public static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(2);

    /// <summary>1.0 up to OverlayHold, then falling in a straight line to 0.0 at OverlayHold + FadeDuration.</summary>
    public static double OpacityAt(TimeSpan sinceShown)
    {
        if (sinceShown <= OverlayHold)
        {
            return 1.0;
        }

        if (sinceShown >= OverlayHold + FadeDuration)
        {
            return 0.0;
        }

        return 1.0 - ((sinceShown - OverlayHold) / FadeDuration);
    }
}
