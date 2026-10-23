namespace SteamGate.Core.Rules;

/// <summary>How a change request ended.</summary>
public enum ChangeResult
{
    /// <summary>The change took effect now (a tightening, a cancel, a loosening at cooldown 0, or a weekend change that applied in part).</summary>
    Applied,

    /// <summary>The whole change waits for the cooldown.</summary>
    Pending,

    /// <summary>A weekend-nights change where some days applied now and some wait.</summary>
    Split,

    /// <summary>The request equals the current value; nothing changed.</summary>
    Same,

    /// <summary>A not-counted removal of a folder that is not on the list.</summary>
    NotInList,

    /// <summary>Not allowed, out of range, or no such pending entry; nothing changed.</summary>
    Refused,
}
