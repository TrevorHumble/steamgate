using System.Globalization;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Status;

/// <summary>The two verdict colors on the screen: green for a change that applies now, amber for the rest.</summary>
public enum VerdictKind
{
    Now,
    Wait,
}

/// <summary>What the Rules editor says about a change before it is requested, and the fixed lines under the other sections.</summary>
public static class VerdictLine
{
    /// <summary>
    /// Runs the engine as a dry run and words the result: the text, its color, and whether Request change is enabled.
    /// Nothing is sent and the engine's result is thrown away.
    /// </summary>
    public static (string Text, VerdictKind Kind, bool CanRequest) For(GateSettings current, ChangeRequest request, DateTime utcNow, TimeZoneInfo zone)
    {
        // The number box allows more than the engine does, so this answer comes first.
        if (request.Field == ChangeFields.CooldownHours && request.Number > ChangeFields.MaxCooldownHours)
        {
            return ($"The cooldown can be at most {ChangeFields.MaxCooldownHours} h ({ChangeFields.MaxCooldownHours / 24} days).", VerdictKind.Wait, false);
        }

        var outcome = ChangeEngine.Request(current, request, utcNow, () => Guid.Empty);
        if (outcome.Result is ChangeResult.Refused or ChangeResult.NotInList)
        {
            return (outcome.Text, VerdictKind.Wait, false);
        }

        if (outcome.Result == ChangeResult.Same)
        {
            return ("No change yet.", VerdictKind.Wait, false);
        }

        if (outcome.Loosens || outcome.Result == ChangeResult.Split)
        {
            var applies = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddHours(current.CooldownHours), zone);
            return (string.Create(CultureInfo.InvariantCulture, $"This loosens the rule. It applies {StatusText.Clock(applies)} ({current.CooldownHours} h)."), VerdictKind.Wait, true);
        }

        // A weekend change where some days applied and the rest could not wait: SteamGate's own words.
        if (outcome.Text.Contains(ChangeReply.NotAllowedPart, StringComparison.Ordinal))
        {
            return (outcome.Text, VerdictKind.Wait, true);
        }

        return ("This tightens the rule. It applies now.", VerdictKind.Now, true);
    }

    /// <summary>
    /// The same verdict for the message the screen would send. A message the service would not read (days that are not day names)
    /// gets the answer the service gives: "Not allowed."
    /// </summary>
    public static (string Text, VerdictKind Kind, bool CanRequest) ForMessage(GateSettings current, RequestMessage message, DateTime utcNow, TimeZoneInfo zone) =>
        ChangeMessages.ToRequest(message) is { } request
            ? For(current, request, utcNow, zone)
            : (ChangeReply.NotAllowed, VerdictKind.Wait, false);

    /// <summary>The line in the add box: "Adding a program loosens the rules. It waits 48 h."</summary>
    public static string NotCountedAdd(int cooldownHours) => "Adding a program loosens the rules. It waits " + StatusText.Hours(cooldownHours) + ".";

    /// <summary>The note beside the turn-off buttons: "Both wait 48 h."</summary>
    public static string TurnOffOrRemove(int cooldownHours) => "Both wait " + StatusText.Hours(cooldownHours) + ".";
}
