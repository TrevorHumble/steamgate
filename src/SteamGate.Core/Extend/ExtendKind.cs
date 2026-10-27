namespace SteamGate.Core.Extend;

/// <summary>Where tonight stands for the Extend button.</summary>
public enum ExtendKind
{
    /// <summary>An extension was already taken tonight.</summary>
    Used,

    /// <summary>Tonight's original limit is more than ExtendRules.WindowSeconds away, or already reached.</summary>
    NotInWindow,

    /// <summary>In the last 30 minutes before the limit, with no extension taken yet.</summary>
    Open,
}
