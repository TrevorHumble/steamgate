namespace SteamGate.Core.Extend;

/// <summary>What the Extend section offers: the kind, the lengths that can be chosen (empty unless Open), and the note under the buttons.</summary>
public sealed record ExtendView(ExtendKind Kind, IReadOnlyList<int> Offered, string Note);
