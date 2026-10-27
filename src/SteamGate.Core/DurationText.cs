using System.Globalization;

namespace SteamGate.Core;

/// <summary>The hours-and-minutes form that the status screen and the Extend texts share.</summary>
public static class DurationText
{
    /// <summary>"45 min", "1 h", "1 h 30 min": under an hour in minutes, else whole hours with the minutes left over when there are any.</summary>
    public static string Minutes(int minutes) =>
        minutes < 60
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes} min")
            : minutes % 60 == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h")
                : string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h {minutes % 60} min");
}
