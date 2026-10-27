using SteamGate.Core.Accounting;
using SteamGate.Core.Rules;

namespace SteamGate.Core.Extend;

/// <summary>
/// Who may extend tonight, and by how much. A pure function of its inputs; the service re-checks everything from its own tally and settings.
/// </summary>
public static class ExtendRules
{
    /// <summary>The extension can be taken when tonight's original limit is at most this many seconds away.</summary>
    public const int WindowSeconds = 1800;

    /// <summary>The length picked at first when all three can be chosen: the middle one of the three. It is one of NightExtension.Lengths.</summary>
    public const int DefaultLength = 20;

    /// <summary>
    /// What the section offers. Open when no extension is taken and the limit is more than 0 and at most WindowSeconds away. A length is offered
    /// only if the week left after tonight's own remaining time covers the whole extension at its higher cost, so the weekly cap stays hard.
    /// </summary>
    public static ExtendView View(double originalLimitSeconds, int usedMinutes, double nightSeconds, int weeklyCapMinutes, double weekSeconds)
    {
        if (usedMinutes > 0)
        {
            return new ExtendView(ExtendKind.Used, [], ExtendText.UsedNote(usedMinutes));
        }

        var tonightLeft = originalLimitSeconds - nightSeconds;
        if (tonightLeft <= 0 || tonightLeft > WindowSeconds)
        {
            return new ExtendView(ExtendKind.NotInWindow, [], ExtendText.NotInWindowNote);
        }

        var weekLeft = (weeklyCapMinutes * 60.0) - weekSeconds;
        var offered = NightExtension.Lengths.Where(length => weekLeft - tonightLeft >= ExtensionCost.FullExtensionWeekSeconds(length)).ToList();
        return new ExtendView(ExtendKind.Open, offered, offered.Count == NightExtension.Lengths.Count ? ExtendText.OpenNote : ExtendText.WeekNote);
    }

    /// <summary>
    /// Takes the extension when every rule allows it. Checks in order: the length, the kind of night (View's note is the refusal), the week for
    /// that length. The tally is rolled over to tonight first. On refusal Updated is the tally that came in.
    /// </summary>
    public static ExtendOutcome Apply(Tally tally, GateSettings settings, DateTime localNow, int minutes)
    {
        if (!NightExtension.Lengths.Contains(minutes))
        {
            return Refused(tally, ChangeReply.NotAllowed);
        }

        var rolled = NightClock.RollOver(tally, localNow);
        var originalLimit = Budget.OriginalLimitSeconds(settings, localNow);
        var view = View(
            originalLimit,
            rolled.ExtensionMinutes,
            rolled.NightSeconds,
            settings.WeeklyCapMinutes,
            rolled.WeekSeconds);
        if (view.Kind != ExtendKind.Open)
        {
            return Refused(tally, view.Note);
        }

        if (!view.Offered.Contains(minutes))
        {
            return Refused(tally, ExtendText.WeekNote);
        }

        return new ExtendOutcome(
            rolled with { Extension = new NightExtension(rolled.Night, minutes) },
            true,
            ExtendText.Applied(minutes, (int)(originalLimit / 60)));
    }

    private static ExtendOutcome Refused(Tally tally, string text) => new(tally, false, text);
}
