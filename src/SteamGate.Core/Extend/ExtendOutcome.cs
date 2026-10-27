using SteamGate.Core.Accounting;

namespace SteamGate.Core.Extend;

/// <summary>The result of an extend request: the tally to keep (the input when refused), whether it applied, and the text to answer with.</summary>
public sealed record ExtendOutcome(Tally Updated, bool Applied, string Text);
