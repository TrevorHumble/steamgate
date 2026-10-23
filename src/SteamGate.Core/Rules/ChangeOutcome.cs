namespace SteamGate.Core.Rules;

/// <summary>
/// What the engine decided for one request or cancel. Updated is the settings after it (the input object when nothing changed).
/// Loosens is true when the request would loosen a rule, whether it waits or applies now at cooldown 0.
/// </summary>
public sealed record ChangeOutcome(GateSettings Updated, ChangeResult Result, string Text, Guid? PendingId, bool Changed, bool Loosens)
{
    /// <summary>False for a refused request (not allowed, out of range, no such entry); the reply's ok flag.</summary>
    public bool Ok => Result != ChangeResult.Refused;
}
