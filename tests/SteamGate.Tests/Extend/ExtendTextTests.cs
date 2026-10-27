using SteamGate.Core.Extend;
using Xunit;

namespace SteamGate.Tests.Extend;

public sealed class ExtendTextTests
{
    [Fact]
    public void Texts()
    {
        Assert.Equal("Extend tonight", ExtendText.Heading);
        Assert.Equal("Finish your round:", ExtendText.RoundLabel);
        Assert.Equal("Extend", ExtendText.ExtendButton);
        Assert.Equal("20 min", ExtendText.Length(20));
        Assert.Equal(
            "Adds 20 min to tonight (limit becomes 1 h 50 min). Up to 40 min comes off this week, because time past tonight's limit counts double.",
            ExtendText.Explain(20, 90));
        Assert.Equal(
            "Adds 30 min to tonight (limit becomes 3 h 30 min). Up to 60 min comes off this week, because time past tonight's limit counts double.",
            ExtendText.Explain(30, 180));
        Assert.Equal(
            "Extension used tonight: 20 min. Only the minutes you actually play past the old limit are counted double.",
            ExtendText.DoneNote(20));
        Assert.Equal(" (20 min extension)", ExtendText.TonightSuffix(20));
        Assert.Equal(" (past the old limit counts double)", ExtendText.WeekSuffix);
        Assert.Equal("Applied now. Tonight ends ", ExtendText.AppliedPrefix);
        Assert.Equal("Not enough time left this week.", ExtendText.WeekNote);
        Assert.Equal("Extension used tonight: 20 min.", ExtendText.UsedNote(20));
        Assert.Equal("Applied now. Tonight ends 20 min later (limit 1 h 50 min).", ExtendText.Applied(20, 90));
        Assert.Equal("Extend is available in the last 30 minutes before tonight's limit.", ExtendText.NotInWindowNote);
        Assert.Equal("Once per night, in the last 30 minutes before tonight's limit.", ExtendText.OpenNote);
    }
}
