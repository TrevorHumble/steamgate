using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusRequestsTests
{
    private static readonly Guid Pending = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void LinesMatchServerMapping()
    {
        var number = Parse<RequestMessage>(StatusRequests.Number("s1", ChangeFields.WeeknightMinutes, 120));
        var days = Parse<RequestMessage>(StatusRequests.WeekendNights("s2", [DayOfWeek.Sunday, DayOfWeek.Saturday]));
        var folder = Parse<RequestMessage>(StatusRequests.Folder("s3", ChangeFields.NotCountedAdd, "Viewfinder"));
        var flag = Parse<RequestMessage>(StatusRequests.Flag("s4", ChangeFields.Enabled, false));
        var cancel = Parse<CancelMessage>(StatusRequests.Cancel("s5", Pending));

        Assert.Equal(("s1", "weeknightMinutes", 120), (number.RequestId, number.Field, number.Number));
        Assert.Equal(("s2", "weekendNights"), (days.RequestId, days.Field));
        Assert.Equal(["Saturday", "Sunday"], days.Days);
        Assert.Equal(("s3", "notCountedAdd", "Viewfinder"), (folder.RequestId, folder.Field, folder.Folder));
        Assert.Equal(("s4", "enabled", false), (flag.RequestId, flag.Field, flag.Flag));
        Assert.Equal(("s5", Pending), (cancel.RequestId, cancel.Id));

        var mappedNumber = ChangeMessages.ToRequest(number)!;
        Assert.Equal(("weeknightMinutes", 120), (mappedNumber.Field, mappedNumber.Number));
        var mappedDays = ChangeMessages.ToRequest(days)!;
        Assert.Equal("weekendNights", mappedDays.Field);
        Assert.Equal([DayOfWeek.Saturday, DayOfWeek.Sunday], mappedDays.Days);
        var mappedFolder = ChangeMessages.ToRequest(folder)!;
        Assert.Equal(("notCountedAdd", "Viewfinder"), (mappedFolder.Field, mappedFolder.Folder));
        var mappedFlag = ChangeMessages.ToRequest(flag)!;
        Assert.Equal(("enabled", false), (mappedFlag.Field, mappedFlag.Flag));
    }

    [Fact]
    public void IdsAreShortAndCountUp()
    {
        Assert.Equal("s1", StatusRequests.Id(1));
        Assert.Equal("s42", StatusRequests.Id(42));
        Assert.True(StatusRequests.Id(int.MaxValue).Length <= ConnectionRules.MaxRequestIdLength);
    }

    [Fact]
    public void WeekendNightsAreSentMondayFirstWithoutRepeats()
    {
        var message = StatusRequests.WeekendNights("s1", [DayOfWeek.Sunday, DayOfWeek.Friday, DayOfWeek.Friday, DayOfWeek.Monday]);

        Assert.Equal(["Monday", "Friday", "Sunday"], message.Days);
    }

    private static T Parse<T>(PipeMessage message)
        where T : PipeMessage =>
        Assert.IsType<T>(PipeMessage.Parse(message.Serialize()));
}
