using System.Globalization;

namespace SteamGate.Core.Pipe;

/// <summary>The words on the overlay.</summary>
public static class BannerText
{
    public static string ForWarn(int minutesLeft) =>
        minutesLeft == 1
            ? "1 minute of gaming left."
            : string.Create(CultureInfo.InvariantCulture, $"{minutesLeft} minutes of gaming left.");

    public static string ForClose(string displayName) => "Time is up. Closing " + displayName + ".";
}
