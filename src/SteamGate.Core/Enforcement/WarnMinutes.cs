namespace SteamGate.Core.Enforcement;

/// <summary>The number shown in a warning.</summary>
public static class WarnMinutes
{
    /// <summary>Whole minutes left, rounded up. Zero or less gives 0.</summary>
    public static int FromRemaining(double remainingSeconds) =>
        (int)Math.Ceiling(Math.Max(0, remainingSeconds) / 60.0);
}
