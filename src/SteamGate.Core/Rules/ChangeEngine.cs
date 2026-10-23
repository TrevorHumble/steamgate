namespace SteamGate.Core.Rules;

/// <summary>
/// The lock: the pure rules for every change. A change that loosens a rule waits out the cooldown as a pending entry; a change
/// that tightens applies at once. Nothing here reads a clock, a file, or a random value; the caller passes them in.
/// What each field is (kind, range, setters, direction) comes from the table in ChangeFields.
/// </summary>
public static class ChangeEngine
{
    /// <summary>Decides one request. Never throws for any ChangeRequest.</summary>
    public static ChangeOutcome Request(GateSettings s, ChangeRequest r, DateTime utcNow, Func<Guid> newId)
    {
        if (ChangeFields.Find(r.Field) is not { Requestable: true } spec || !spec.HasOnlyMember(r.Number, r.Flag, r.Folder, r.Days))
        {
            return Refuse(s, ChangeReply.NotAllowed);
        }

        return spec.Kind switch
        {
            FieldKind.Number => RequestNumber(s, spec, r.Number!.Value, utcNow, newId),
            FieldKind.Flag => RequestFlag(s, spec, r.Flag!.Value, utcNow, newId),
            FieldKind.Folder => spec.Name == ChangeFields.NotCountedAdd
                ? RequestFolderAdd(s, r.Folder!, utcNow, newId)
                : RequestFolderRemove(s, r.Folder!),
            _ => RequestWeekendNights(s, r.Days!, utcNow, newId),
        };
    }

    /// <summary>Removes a pending entry at once. An unknown id changes nothing.</summary>
    public static ChangeOutcome Cancel(GateSettings s, Guid id)
    {
        if (!s.Pending.Any(pending => pending.Id == id))
        {
            return Refuse(s, ChangeReply.NoPendingChange);
        }

        var updated = s with { Pending = [.. s.Pending.Where(pending => pending.Id != id)] };
        return new ChangeOutcome(updated, ChangeResult.Applied, ChangeReply.AppliedNow, null, Changed: true, Loosens: false);
    }

    /// <summary>
    /// The seconds to take off every pending entry this tick. While the service runs it is the change in system uptime, which keeps
    /// counting through sleep and ignores the clock. On the first tick it is the time since the last save: by uptime on the same Windows
    /// boot (a clock change gains nothing), by the clock after a restart of Windows. Never below 0.
    /// </summary>
    public static double StepSeconds(bool firstTick, bool sameBoot, DateTime utcNow, long prevUptimeMs, long uptimeMs, DateTime? lastSavedUtc, long? lastSavedUptimeMs)
    {
        if (!firstTick)
        {
            return Math.Max(0, (uptimeMs - prevUptimeMs) / 1000.0);
        }

        if (sameBoot)
        {
            return lastSavedUptimeMs is { } savedUptime ? Math.Max(0, (uptimeMs - savedUptime) / 1000.0) : 0;
        }

        return lastSavedUtc is { } savedUtc ? Math.Max(0, (utcNow - savedUtc).TotalSeconds) : 0;
    }

    /// <summary>Takes the seconds off every pending entry's remaining wait.</summary>
    public static GateSettings Countdown(GateSettings s, double seconds)
    {
        if (s.Pending.Count == 0 || !(seconds > 0))
        {
            return s;
        }

        return s with { Pending = [.. s.Pending.Select(entry => entry with { RemainingWaitSeconds = entry.RemainingWaitSeconds - seconds })] };
    }

    /// <summary>
    /// Applies every entry whose wait is over, oldest request first (then by Id), so the later request wins. An entry that can no longer be
    /// applied (a not-counted add that would pass 50 folders, or one the engine does not understand) is removed and returned in Skipped. Never throws.
    /// </summary>
    public static (GateSettings Updated, IReadOnlyList<PendingChange> Applied, IReadOnlyList<PendingChange> Skipped) ApplyDue(GateSettings s)
    {
        var due = s.Pending.Where(entry => entry.RemainingWaitSeconds <= 0).OrderBy(entry => entry.RequestedUtc).ThenBy(entry => entry.Id).ToList();
        if (due.Count == 0)
        {
            return (s, [], []);
        }

        var current = s;
        var applied = new List<PendingChange>();
        var skipped = new List<PendingChange>();
        foreach (var entry in due)
        {
            if (TryApply(current, entry, out var next))
            {
                current = next;
                applied.Add(entry);
            }
            else
            {
                skipped.Add(entry);
            }
        }

        var waiting = s.Pending.Where(entry => !(entry.RemainingWaitSeconds <= 0)).ToList();
        return (current with { Pending = waiting }, applied, skipped);
    }

    private static bool TryApply(GateSettings s, PendingChange entry, out GateSettings next)
    {
        next = s;
        if (ChangeFields.Find(entry.Field) is not { Pendable: true } spec || !spec.HasOnlyMember(entry.Number, entry.Flag, entry.Folder, entry.Days))
        {
            return false;
        }

        switch (spec.Kind)
        {
            case FieldKind.Number:
                next = spec.SetNumber!(s, entry.Number!.Value);
                return true;
            case FieldKind.Flag:
                next = spec.SetFlag!(s, entry.Flag!.Value);
                return true;
            case FieldKind.Folder:
                if (OnList(s, entry.Folder!))
                {
                    return true;
                }

                if (s.NotCounted.Count >= ChangeFields.MaxNotCounted)
                {
                    return false;
                }

                next = s with { NotCounted = [.. s.NotCounted, entry.Folder!] };
                return true;
            default:
                next = s with
                {
                    WeekendNights = spec.Name == ChangeFields.WeekendNightsAdd
                        ? DayNames.MondayFirst(s.WeekendNights.Concat(entry.Days!))
                        : DayNames.MondayFirst(s.WeekendNights.Where(night => !entry.Days!.Contains(night))),
                };
                return true;
        }
    }

    private static ChangeOutcome RequestNumber(GateSettings s, FieldSpec spec, int value, DateTime utcNow, Func<Guid> newId)
    {
        if (!spec.InRange(value))
        {
            return Refuse(s, ChangeReply.OutOfRange(0, spec.Max));
        }

        var current = spec.GetNumber!(s);
        if (value == current)
        {
            return Same(s);
        }

        var updated = spec.SetNumber!(s, value);
        var loosens = spec.HigherLoosens ? value > current : value < current;
        return loosens
            ? Loosen(s, updated, new PendingChange(Guid.Empty, spec.Name, value, null, null, null, utcNow, 0), utcNow, newId)
            : Tighten(updated);
    }

    private static ChangeOutcome RequestFlag(GateSettings s, FieldSpec spec, bool value, DateTime utcNow, Func<Guid> newId)
    {
        if (value == spec.GetFlag!(s))
        {
            return Same(s);
        }

        var updated = spec.SetFlag!(s, value);
        return value == spec.LoosensWhenTrue
            ? Loosen(s, updated, new PendingChange(Guid.Empty, spec.Name, null, value, null, null, utcNow, 0), utcNow, newId)
            : Tighten(updated);
    }

    private static ChangeOutcome RequestFolderAdd(GateSettings s, string folder, DateTime utcNow, Func<Guid> newId)
    {
        if (!ChangeFields.IsValidFolder(folder))
        {
            return Refuse(s, ChangeReply.NotAllowed);
        }

        if (OnList(s, folder))
        {
            return Same(s);
        }

        var waitingAdds = s.Pending.Count(entry => entry.Field == ChangeFields.NotCountedAdd);
        if (s.NotCounted.Count + waitingAdds >= ChangeFields.MaxNotCounted)
        {
            return Refuse(s, ChangeReply.NotAllowed);
        }

        var updated = s with { NotCounted = [.. s.NotCounted, folder] };
        return Loosen(s, updated, new PendingChange(Guid.Empty, ChangeFields.NotCountedAdd, null, null, folder, null, utcNow, 0), utcNow, newId);
    }

    private static ChangeOutcome RequestFolderRemove(GateSettings s, string folder)
    {
        if (!ChangeFields.IsValidFolder(folder))
        {
            return Refuse(s, ChangeReply.NotAllowed);
        }

        if (!OnList(s, folder))
        {
            return new ChangeOutcome(s, ChangeResult.NotInList, ChangeReply.NotInList, null, Changed: false, Loosens: false);
        }

        return Tighten(s with { NotCounted = [.. s.NotCounted.Where(name => !string.Equals(name, folder, StringComparison.OrdinalIgnoreCase))] });
    }

    // Each day is judged on its own: an added night always waits; a removed night applies now only when it cannot raise that night's limit.
    private static ChangeOutcome RequestWeekendNights(GateSettings s, IReadOnlyList<DayOfWeek> requested, DateTime utcNow, Func<Guid> newId)
    {
        if (!ChangeFields.AreDistinctDays(requested))
        {
            return Refuse(s, ChangeReply.NotAllowed);
        }

        var adds = DayNames.MondayFirst(requested.Where(day => !s.WeekendNights.Contains(day)));
        var removes = DayNames.MondayFirst(s.WeekendNights.Where(day => !requested.Contains(day)));
        if (adds.Count == 0 && removes.Count == 0)
        {
            return Same(s);
        }

        // A removal that cannot raise a limit is a tightening and applies now; the rest is a loosening.
        var removalIsSafe = RemovalCannotRaiseALimit(s);
        var loosening = new List<PendingChange>();
        if (adds.Count > 0)
        {
            loosening.Add(new PendingChange(Guid.Empty, ChangeFields.WeekendNightsAdd, null, null, null, adds, utcNow, 0));
        }

        if (!removalIsSafe && removes.Count > 0)
        {
            loosening.Add(new PendingChange(Guid.Empty, ChangeFields.WeekendNightsRemove, null, null, null, removes, utcNow, 0));
        }

        var plan = Plan(s, loosening.Count);
        var applyAdds = plan == Schedule.ApplyNow ? adds : [];
        var applyRemoves = plan == Schedule.ApplyNow || removalIsSafe ? removes : [];
        var waiting = plan == Schedule.ApplyNow ? [] : DayNames.MondayFirst(loosening.SelectMany(entry => entry.Days!));
        var applied = DayNames.MondayFirst(applyAdds.Concat(applyRemoves));
        var now = s with { WeekendNights = DayNames.MondayFirst(s.WeekendNights.Concat(applyAdds).Where(night => !applyRemoves.Contains(night))) };

        if (plan == Schedule.Full)
        {
            // The waiting days cannot get an entry; whatever applies now still does.
            return applied.Count == 0
                ? Refuse(s, ChangeReply.NotAllowed)
                : new ChangeOutcome(now, ChangeResult.Applied, ChangeReply.AppliedNowAndNotAllowed(applied, waiting), null, Changed: true, Loosens: false);
        }

        if (plan == Schedule.ApplyNow)
        {
            var loosens = loosening.Count > 0;
            return new ChangeOutcome(now, ChangeResult.Applied, ChangeReply.AppliedNow, null, Changed: true, Loosens: loosens);
        }

        var seconds = CooldownSeconds(s);
        var entries = MakeEntries(s, loosening, utcNow, newId);
        var updated = now with { Pending = [.. s.Pending, .. entries] };
        if (applied.Count == 0)
        {
            return new ChangeOutcome(updated, ChangeResult.Pending, ChangeReply.Waits(seconds), entries[0].Id, Changed: true, Loosens: true);
        }

        return new ChangeOutcome(updated, ChangeResult.Split, ChangeReply.AppliedNowAndWaits(applied, seconds, waiting), entries[0].Id, Changed: true, Loosens: true);
    }

    // Under every mix of the current and the pending limits, the weekend limit stays at least the weeknight limit, so a night
    // that moves from the weekend limit to the weeknight limit never gets more time.
    private static bool RemovalCannotRaiseALimit(GateSettings s)
    {
        var weeknight = s.Pending.Where(entry => entry.Field == ChangeFields.WeeknightMinutes && entry.Number is not null).Select(entry => entry.Number!.Value).Append(s.WeeknightMinutes).Max();
        var weekend = s.Pending.Where(entry => entry.Field == ChangeFields.WeekendNightMinutes && entry.Number is not null).Select(entry => entry.Number!.Value).Append(s.WeekendNightMinutes).Min();
        return weekend >= weeknight;
    }

    private static ChangeOutcome Tighten(GateSettings updated) =>
        new(updated, ChangeResult.Applied, ChangeReply.AppliedNow, null, Changed: true, Loosens: false);

    private static ChangeOutcome Same(GateSettings s) =>
        new(s, ChangeResult.Same, ChangeReply.AlreadySet, null, Changed: false, Loosens: false);

    private static ChangeOutcome Refuse(GateSettings s, string text) =>
        new(s, ChangeResult.Refused, text, null, Changed: false, Loosens: false);

    // What a loosening does: applies now at cooldown 0, is refused when it needs more pending slots than are free, or waits.
    private enum Schedule
    {
        ApplyNow,
        Full,
        Waits,
    }

    private static Schedule Plan(GateSettings s, int entriesNeeded)
    {
        // Nothing to wait for, or no cooldown to wait out.
        if (entriesNeeded == 0 || s.CooldownHours == 0)
        {
            return Schedule.ApplyNow;
        }

        return s.Pending.Count + entriesNeeded > ChangeFields.MaxPending ? Schedule.Full : Schedule.Waits;
    }

    // Gives each template its id, request time, and the full cooldown in force as its wait. The id source is called once per entry, in order.
    private static List<PendingChange> MakeEntries(GateSettings s, IEnumerable<PendingChange> templates, DateTime utcNow, Func<Guid> newId) =>
        [.. templates.Select(template => template with { Id = newId(), RequestedUtc = utcNow, RemainingWaitSeconds = CooldownSeconds(s) })];

    // The full cooldown in force, in seconds: what a new pending entry waits.
    private static double CooldownSeconds(GateSettings s) => s.CooldownHours * 3600.0;

    private static ChangeOutcome Loosen(GateSettings s, GateSettings appliedNow, PendingChange template, DateTime utcNow, Func<Guid> newId)
    {
        switch (Plan(s, 1))
        {
            case Schedule.ApplyNow:
                return new ChangeOutcome(appliedNow, ChangeResult.Applied, ChangeReply.AppliedNow, null, Changed: true, Loosens: true);
            case Schedule.Full:
                return Refuse(s, ChangeReply.NotAllowed);
            default:
                var entry = MakeEntries(s, [template], utcNow, newId)[0];
                return new ChangeOutcome(s with { Pending = [.. s.Pending, entry] }, ChangeResult.Pending, ChangeReply.Waits(entry.RemainingWaitSeconds), entry.Id, Changed: true, Loosens: true);
        }
    }

    private static bool OnList(GateSettings s, string folder) =>
        s.NotCounted.Any(name => string.Equals(name, folder, StringComparison.OrdinalIgnoreCase));
}
