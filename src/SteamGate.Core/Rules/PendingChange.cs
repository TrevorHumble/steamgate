namespace SteamGate.Core.Rules;

/// <summary>
/// A loosening that waits out the cooldown. Exactly one value member is set, chosen by Field. RemainingWaitSeconds starts at the
/// cooldown in force when the request came and only counts down. Days (for weekendNightsAdd and weekendNightsRemove) hold only the days that wait.
/// </summary>
public sealed record PendingChange(
    Guid Id,
    string Field,
    int? Number,
    bool? Flag,
    string? Folder,
    IReadOnlyList<DayOfWeek>? Days,
    DateTime RequestedUtc,
    double RemainingWaitSeconds)
{
    /// <summary>
    /// True when Field is a pending field, exactly the value member that matches it is set and in range, and the wait is a number from 0 up to the
    /// longest cooldown. A negative wait is refused: an entry that is due is applied and removed in the same tick, so one is never saved.
    /// </summary>
    public bool IsValid() =>
        ChangeFields.Find(Field) is { Pendable: true } spec
        && spec.IsValidValue(Number, Flag, Folder, Days)
        && RemainingWaitSeconds >= 0
        && RemainingWaitSeconds <= ChangeFields.MaxWaitSeconds;
}
