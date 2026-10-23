namespace SteamGate.Core.Rules;

/// <summary>
/// One change a client asks for: a field name and a new value. Exactly one value member is set, the one that matches Field;
/// anything else is answered "Not allowed."
/// </summary>
public sealed record ChangeRequest(string Field, int? Number, bool? Flag, string? Folder, IReadOnlyList<DayOfWeek>? Days);
